// El almacén de ámbitos (contrato 05 §5 y §7): foto, delta, revocado, niebla parcial, plaza con máscara de campos, incoherencias y el `Hola`
// de quien reanuda. Sin servidor ni escena.
using System;
using BronzeAge.Contract.V1;
using ConquestTactics.Mundo;
using Google.Protobuf;
using NUnit.Framework;
using AmbitoPb = BronzeAge.Contract.V1.Ambito;

namespace ConquestTactics.Mundo.Tests
{
    public class AlmacenTests
    {
        static AmbitoPb Amb(AmbitoTipo t, string id) => new AmbitoPb { Tipo = t, Id = id };
        static Entidad Heroe(string id, string nombre) => new Entidad { Heroe = new Heroe { Id = id, DisplayName = nombre } };
        static Entidad Plaza(string id) => new Entidad { Asentamiento = new Asentamiento { Id = id } };
        static MensajeDeServidor M(Foto f) => new MensajeDeServidor { Foto = f };
        static MensajeDeServidor M(Delta d) => new MensajeDeServidor { Delta = d };

        static Delta D(string heroe, ulong desde, ulong hasta, params Cambio[] cambios)
        {
            var d = new Delta { Ambito = Amb(AmbitoTipo.AmbitoHeroe, heroe), Desde = desde, Hasta = hasta };
            d.Cambios.AddRange(cambios);
            return d;
        }

        Almacen ConFoto(params Entidad[] entidades)
        {
            var a = new Almacen();
            var f = new Foto { Ambito = Amb(AmbitoTipo.AmbitoHeroe, "heroe-1"), Version = 5 };
            f.Entidades.AddRange(entidades);
            Assert.AreEqual("HEROE:heroe-1", a.Aplicar(M(f)));
            return a;
        }

        [Test]
        public void LaFotoSustituyeAlAmbitoYCadaClaseTieneSuClave()
        {
            var a = ConFoto(Heroe("heroe-1", "Ana"), Plaza("as-1"), new Entidad { RedCaminos = new RedCaminos() });
            var amb = a.Ambitos["HEROE:heroe-1"];
            Assert.AreEqual(5UL, amb.Version);
            Assert.AreEqual(3, amb.Entidades.Count);
            Assert.IsTrue(amb.Entidades.ContainsKey((TipoEntidad.RedCaminos, "red")));

            var otra = new Foto { Ambito = Amb(AmbitoTipo.AmbitoHeroe, "heroe-1"), Version = 9 };
            otra.Entidades.Add(Heroe("heroe-1", "Ana"));
            a.Aplicar(M(otra));
            Assert.AreEqual(1, a.Ambitos["HEROE:heroe-1"].Entidades.Count, "la foto nueva no suma: sustituye");
        }

        [Test]
        public void LasClavesDeAmbitoSonLasDelContrato()
        {
            // El orden de `AmbitoTipo` no es el de la lista a mano de BronzeAgeClient (`claveCorta`): FACCION es el 5, VISTA el 6, ADMIN el 7.
            Assert.AreEqual("FACCION:f-1", Almacen.ClaveDeAmbito(AmbitoTipo.AmbitoFaccion, "f-1"));
            Assert.AreEqual("VISTA:f-1", Almacen.ClaveDeAmbito(AmbitoTipo.AmbitoVista, "f-1"));
            Assert.AreEqual("CAMPAMENTO:camp-0", Almacen.ClaveDeAmbito(AmbitoTipo.AmbitoCampamento, "camp-0"));
            Assert.AreEqual("ADMIN:", Almacen.ClaveDeAmbito(AmbitoTipo.AmbitoAdmin, ""));
        }

        [Test]
        public void LaDeltaReemplazaQuitaYAvanzaLaVersion()
        {
            var a = ConFoto(Heroe("heroe-1", "Ana"), Plaza("as-1"));
            var d = D("heroe-1", 5, 7,
                new Cambio { Tipo = TipoCambio.CambioActualiza, Entidad = TipoEntidad.Heroe, Id = "heroe-1", Reemplaza = true, Datos = Heroe("heroe-1", "Ana la Brava") },
                new Cambio { Tipo = TipoCambio.CambioQuita, Entidad = TipoEntidad.Asentamiento, Id = "as-1" },
                new Cambio { Tipo = TipoCambio.CambioQuita, Entidad = TipoEntidad.Ejercito, Id = "ej-que-nunca-tuvo" });
            d.Eventos.Add(new EventoDominio { Codigo = "heroe.creado" });
            a.Aplicar(M(d));
            var amb = a.Ambitos["HEROE:heroe-1"];
            Assert.AreEqual(7UL, amb.Version);
            Assert.AreEqual("Ana la Brava", amb.Entidades[(TipoEntidad.Heroe, "heroe-1")].Heroe.DisplayName);
            Assert.IsFalse(amb.Entidades.ContainsKey((TipoEntidad.Asentamiento, "as-1")));
            Assert.AreEqual(1, a.Hechos.Count);
            Assert.AreEqual("HEROE:heroe-1", a.Hechos[0].ambito);
        }

        [Test]
        public void UnaDeltaConHuecoOSinAmbitoEsIncoherente()
        {
            var a = ConFoto(Heroe("heroe-1", "Ana"));
            var e = Assert.Throws<Incoherencia>(() => a.Aplicar(M(D("heroe-1", 6, 7))));
            Assert.AreEqual("HEROE:heroe-1", e.Ambito, "dice qué ámbito tirar");
            Assert.Throws<Incoherencia>(() => a.Aplicar(M(D("heroe-2", 0, 1))));
            Assert.Throws<Incoherencia>(() => a.Aplicar(M(D("heroe-1", 5, 4))));
            Assert.Throws<Incoherencia>(() => a.Aplicar(M(D("heroe-1", 5, 6,
                new Cambio { Tipo = TipoCambio.CambioActualiza, Entidad = TipoEntidad.Heroe, Id = "heroe-1", Reemplaza = false, Datos = Heroe("heroe-1", "x") }))),
                "solo la plaza viaja parcial");
        }

        [Test]
        public void ElRevocadoDescartaElAmbito()
        {
            var a = ConFoto(Heroe("heroe-1", "Ana"));
            Assert.AreEqual("HEROE:heroe-1", a.Aplicar(new MensajeDeServidor { Revocado = new Revocado { Ambito = Amb(AmbitoTipo.AmbitoHeroe, "heroe-1") } }));
            Assert.IsEmpty(a.Ambitos);
        }

        [Test]
        public void LaPlazaParcialCopiaSoloLosCamposDeLaMascara()
        {
            var plaza = Plaza("as-1");
            plaza.Asentamiento.Nombre = "Micenas";
            plaza.Asentamiento.Nivel = 1;
            plaza.Asentamiento.FundadoresIds.Add("heroe-1");
            plaza.Asentamiento.CasasCompradas.Add("heroe-9");
            plaza.Asentamiento.Almacen["madera"] = new Existencia();
            var a = ConFoto(plaza);

            var parcial = new Asentamiento { Id = "as-1", Nivel = 2 }; // sin nombre: el optional ausente se borra
            parcial.FundadoresIds.Add("heroe-2");
            parcial.Almacen["piedra"] = new Existencia();
            var c = new Cambio { Tipo = TipoCambio.CambioActualiza, Entidad = TipoEntidad.Asentamiento, Id = "as-1", Datos = new Entidad { Asentamiento = parcial } };
            c.Campos.AddRange(new[] { "version", "version_interior", "nivel", "nombre", "fundadores_ids", "almacen", "un_campo_del_futuro" });
            a.Aplicar(M(D("heroe-1", 5, 6, c)));

            var p = a.Ambitos["HEROE:heroe-1"].Entidades[(TipoEntidad.Asentamiento, "as-1")].Asentamiento;
            Assert.AreEqual(2u, p.Nivel);
            Assert.IsFalse(p.HasNombre, "el optional ausente en la máscara se borra");
            CollectionAssert.AreEqual(new[] { "heroe-2" }, p.FundadoresIds, "la lista se sustituye entera");
            CollectionAssert.AreEquivalent(new[] { "piedra" }, p.Almacen.Keys, "el mapa también");
            CollectionAssert.AreEqual(new[] { "heroe-9" }, p.CasasCompradas, "lo que no está en la máscara no se toca");
        }

        [Test]
        public void LaNieblaParcialSobrescribeSusTramos()
        {
            var foto = new Foto { Ambito = Amb(AmbitoTipo.AmbitoVista, "f-1"), Version = 1, Niebla = new NieblaProyectada { Columnas = 16, Filas = 2, Celdas = ByteString.CopyFrom(new byte[4]), Visibles = ByteString.CopyFrom(new byte[4]) } };
            var a = new Almacen();
            a.Aplicar(M(foto));
            var n = new NieblaProyectada { Parcial = true };
            n.TramosCeldas.Add(new TramoDeNiebla { Desde = 1, Datos = ByteString.CopyFrom(0xFF, 0x01) });
            n.TramosVisibles.Add(new TramoDeNiebla { Desde = 2, Datos = ByteString.CopyFrom(0x01) });
            a.Aplicar(M(new Delta { Ambito = Amb(AmbitoTipo.AmbitoVista, "f-1"), Desde = 1, Hasta = 2, Niebla = n }));

            var niebla = a.Ambitos["VISTA:f-1"].Niebla;
            CollectionAssert.AreEqual(new byte[] { 0, 0xFF, 0x01, 0 }, niebla.Celdas.ToByteArray());
            Assert.AreEqual(0, Almacen.EstadoCelda(niebla, 0, 0), "nunca vista");
            Assert.AreEqual(1, Almacen.EstadoCelda(niebla, 8, 0), "vista alguna vez (byte 1, bit 0)");
            Assert.AreEqual(2, Almacen.EstadoCelda(niebla, 0, 1), "a la vista (byte 2 = fila 1, columna 0)");

            var sinPrevia = new Almacen();
            sinPrevia.Aplicar(M(new Foto { Ambito = Amb(AmbitoTipo.AmbitoVista, "f-1"), Version = 1 }));
            Assert.Throws<Incoherencia>(() => sinPrevia.Aplicar(M(new Delta { Ambito = Amb(AmbitoTipo.AmbitoVista, "f-1"), Desde = 1, Hasta = 2, Niebla = n })));
        }

        [Test]
        public void UnaVistaNuevaSustituyeALaDeOtraPerspectiva()
        {
            var a = new Almacen();
            a.Aplicar(M(new Foto { Ambito = Amb(AmbitoTipo.AmbitoVista, "heroe-1"), Version = 1 }));
            a.Aplicar(M(new Foto { Ambito = Amb(AmbitoTipo.AmbitoVista, "f-1"), Version = 1 }));
            CollectionAssert.AreEquivalent(new[] { "VISTA:f-1" }, a.Ambitos.Keys);
        }

        [Test]
        public void ElHolaDeclaraLoQueSeTieneMenosLaVista()
        {
            var a = new Almacen();
            var plaza = Plaza("as-1");
            plaza.Asentamiento.VersionInterior = 4;
            var f = new Foto { Ambito = Amb(AmbitoTipo.AmbitoAsentamiento, "as-1"), Version = 8 };
            f.Entidades.Add(plaza);
            a.Aplicar(M(f));
            var propia = Plaza("as-2");
            propia.Asentamiento.VersionInterior = 3;
            var ff = new Foto { Ambito = Amb(AmbitoTipo.AmbitoFaccion, "f-1"), Version = 2 };
            ff.Entidades.Add(propia);
            a.Aplicar(M(ff));
            a.Aplicar(M(new Foto { Ambito = Amb(AmbitoTipo.AmbitoVista, "f-1"), Version = 30 }));

            var h = a.Hola("tok", TipoCliente.ClienteUnity, "conquest", null, 0);
            Assert.AreEqual(8UL, h.VersionesConocidas["ASENTAMIENTO:as-1"]);
            Assert.AreEqual(2UL, h.VersionesConocidas["FACCION:f-1"]);
            Assert.IsFalse(h.VersionesConocidas.ContainsKey("VISTA:f-1"));
            Assert.AreEqual(4UL, h.VersionesInterioresConocidas["ASENTAMIENTO:as-1"]);
            Assert.AreEqual(3UL, h.VersionesInterioresConocidas["PROPIA:as-2"]);
            Assert.IsFalse(h.HasMundoHashConocido);
            CollectionAssert.Contains(h.Compresiones, "zstd");
        }

        [Test]
        public void LaRutaSeEvaluaAlInstante()
        {
            var plan = new Plan();
            plan.Vertices.Add(new Vertice { P = new Punto { X = 0, Y = 0 }, EnMs = 1000 });
            plan.Vertices.Add(new Vertice { P = new Punto { X = 10_000, Y = 0 }, EnMs = 3000 });
            var antes = Instante.Posicion(plan, null, 0);
            var medio = Instante.Posicion(plan, null, 2000);
            var despues = Instante.Posicion(plan, null, 5000);
            Assert.AreEqual((0d, 0d, true), antes);
            Assert.AreEqual((5000d, 0d, true), medio);
            Assert.AreEqual((10_000d, 0d, false), despues);
            Assert.AreEqual(2, Instante.RutaRestante(plan, null, 2000).Count);

            var r = new Ritmo { ValorEnT0 = 1000, T0Ms = 0, PorMinuto = 600, Maximo = 1200 };
            Assert.AreEqual(1100, Instante.Valor(r, 10_000), 1e-9);
            Assert.AreEqual(1200, Instante.Valor(r, 600_000), 1e-9, "con techo");
            r.EnEscalones = true;
            Assert.AreEqual(1000, Instante.Valor(r, 10_000), 1e-9, "en escalones no se interpola");
        }

        [Test]
        public void LosRechazosSeExplicanConLoQueFalta()
        {
            Assert.AreEqual("Nick o contraseña incorrectos.", Textos.Explicar("credenciales_invalidas"));
            Assert.AreEqual("codigo.que.no.existe", Textos.Explicar("codigo.que.no.existe"));
            var faltan = new Google.Protobuf.WellKnownTypes.Struct();
            faltan.Fields["madera"] = Google.Protobuf.WellKnownTypes.Value.ForNumber(12_500);
            var detalle = new Google.Protobuf.WellKnownTypes.Struct();
            detalle.Fields["faltan"] = Google.Protobuf.WellKnownTypes.Value.ForStruct(faltan);
            var texto = Textos.Explicar(new RechazoOrden { Codigo = "refundacion.fondo_corto", Detalle = detalle });
            StringAssert.EndsWith("Faltan: madera 12,5.", texto);

            var a = new Almacen();
            a.NombresHeroes["heroe-7"] = "Beto";
            Assert.AreEqual("Beto", Textos.NombreDeHeroe(a, "heroe-7"));
            Assert.AreEqual("Héroe 59", Textos.NombreDeHeroe(a, "heroe-59"));
        }
    }
}

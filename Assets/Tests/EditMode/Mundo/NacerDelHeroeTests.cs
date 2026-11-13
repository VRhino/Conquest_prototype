// Las clases del héroe salen de `/v1/datos` con su equipo inicial (no escritas a mano).
using System.Text;
using NUnit.Framework;

namespace ConquestTactics.Mundo.Tests
{
    public class NacerDelHeroeTests
    {
        const string Datos = @"{ ""documentos"": { ""Objetos_Del_Heroe"": {
            ""CLASES_HEROE"": { ""clases"": [
                { ""id"": ""dagas"", ""nombre"": ""Dagas"", ""armaduras"": [""ligera""], ""armaduraInicial"": ""ligera"",
                  ""atributosBase"": { ""fuerza"": 2, ""destreza"": 5, ""armadura"": 1, ""vitalidad"": 2 } } ] },
            ""OBJETOS"": { ""objetos"": [
                { ""id"": ""dagas_inicio"", ""tipo"": ""arma"", ""clase"": ""dagas"", ""tier"": 1, ""nombre"": ""Dagas de pacotilla"" },
                { ""id"": ""dagas_cobre"", ""tipo"": ""arma"", ""clase"": ""dagas"", ""tier"": 2, ""nombre"": ""Dagas de cobre"" },
                { ""id"": ""torso_ligera_inicio"", ""tipo"": ""armadura"", ""hueco"": ""torso"", ""tipoArmadura"": ""ligera"", ""tier"": 1, ""nombre"": ""Torso de inicio (ligera)"" },
                { ""id"": ""torso_media_inicio"", ""tipo"": ""armadura"", ""hueco"": ""torso"", ""tipoArmadura"": ""media"", ""tier"": 1, ""nombre"": ""Torso de inicio (media)"" },
                { ""id"": ""casco_ligera_inicio"", ""tipo"": ""armadura"", ""hueco"": ""casco"", ""tipoArmadura"": ""ligera"", ""tier"": 1, ""nombre"": ""Casco"" },
                { ""id"": ""botas_ligera_inicio"", ""tipo"": ""armadura"", ""hueco"": ""botas"", ""tipoArmadura"": ""ligera"", ""tier"": 1, ""nombre"": ""Botas de inicio (ligera)"" },
                { ""id"": ""montura_tiro"", ""tipo"": ""montura"", ""tier"": 1, ""nombre"": ""Caballo de tiro"" } ] } } } }";

        [Test]
        public void LasClasesLlevanSuEquipoInicial()
        {
            var clases = ClaseDeHeroe.Leer(DatosDeJuego.Leer(Encoding.UTF8.GetBytes(Datos)));
            Assert.AreEqual(1, clases.Count);
            var c = clases[0];
            Assert.AreEqual("dagas", c.Id);
            Assert.AreEqual(5, c.Atributos["destreza"]);
            CollectionAssert.AreEqual(new[] { "Dagas de pacotilla", "Torso de inicio (ligera)", "Botas de inicio (ligera)", "Caballo de tiro" }, c.EquipoInicial,
                "el arma de tier 1 de su clase, las piezas de su armadura inicial sin casco y la montura de tiro");
            Assert.AreEqual("Armadura ligera; empieza con la ligera", c.TextoDeArmaduras);
            Assert.AreEqual("Campamento 4", AnalisisDeCampamento.Nombre("campamento-4"));
        }

        [Test]
        public void LosCampamentosLlevanElNombreDeSuRegion()
        {
            // Un mundo de 24.000 × 16.000 u: la «Cuenca fértil» está en (0,53, 0,27).
            Assert.AreEqual("Cuenca fértil", AnalisisDeCampamento.Region(0.53 * 24000, 0.27 * 16000, 24000, 16000).nombre);
            var dos = new System.Collections.Generic.List<AnalisisDeCampamento>
            {
                new AnalisisDeCampamento { Id = "camp-1", X = 0.53 * 24000 + 400, Y = 0.27 * 16000 },
                new AnalisisDeCampamento { Id = "camp-2", X = 0.53 * 24000 - 400, Y = 0.27 * 16000 },
            };
            AnalisisDeCampamento.PonerLugares(dos, 24000, 16000);
            Assert.AreEqual("Cuenca fértil (este)", dos[0].Lugar, "dos en la misma región: hacia dónde queda cada uno");
            Assert.AreEqual("Cuenca fértil (oeste)", dos[1].Lugar);
        }

        [Test]
        public void ElRecomendadoEsElMejorValorado()
        {
            var bueno = new AnalisisDeCampamento { Id = "camp-1", Gente = 1, FertilidadMedia = 0.8, Rio = 10 };
            bueno.Nodos["cobre"] = (3, 4, 20);
            var malo = new AnalisisDeCampamento { Id = "camp-2", Gente = 9, FertilidadMedia = 0.2 };
            malo.Bandidos.Add((50, 3));
            var lista = new System.Collections.Generic.List<AnalisisDeCampamento> { malo, bueno };
            AnalisisDeCampamento.Valorar(lista);
            Assert.AreSame(bueno, AnalisisDeCampamento.Recomendado(lista));
            Assert.AreEqual(1, bueno.Nota, 1e-9, "el mejor en todo: nota 1");
            CollectionAssert.AreEqual(new[] { "Poca gente", "Tierra fértil" }, bueno.Razones, "las dos razones que más pesan");
            Assert.AreSame(bueno, AnalisisDeCampamento.ElegirPorMi(lista, 0), "al azar entre los mejores, con más peso el mejor");
        }
    }
}

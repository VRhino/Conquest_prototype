// Los textos que el jugador lee de lo que dice el servidor: los códigos de rechazo y de acceso traducidos a frases (puerto de
// `BronzeAgeClient/src/ui/erroresServidor.ts`; se decide por el código, nunca por el texto: `07_Catalogo_de_Rechazos.md` del motor) y el
// nombre de un héroe en lugar de su id (`MensajeDeServidor.nombres_heroes`, que junta el almacén).
using System.Collections.Generic;
using System.Linq;
using BronzeAge.Contract.V1;

namespace ConquestTactics.Mundo
{
    public static class Textos
    {
        /// <summary>La frase del jugador para un código del servidor; si no se conoce, el propio código.</summary>
        public static string Explicar(string codigo) => codigo != null && Mensajes.TryGetValue(codigo, out var m) ? m : codigo;

        /// <summary>Lo que se le dice al jugador de un rechazo: la frase de su código y, si el detalle lo trae, lo que falta (viaja en milésimas).</summary>
        public static string Explicar(RechazoOrden r)
        {
            if (r == null) return "";
            // Sin frase propia, el texto que da el servidor (como BronzeAgeClient); sin texto, el código.
            var texto = Mensajes.TryGetValue(r.Codigo ?? "", out var m) ? m : !string.IsNullOrEmpty(r.Mensaje) ? r.Mensaje : r.Codigo;
            var faltan = r.Detalle?.Fields.TryGetValue("faltan", out var f) == true ? f.StructValue : null;
            if (faltan != null && faltan.Fields.Count > 0)
                texto += " Faltan: " + string.Join(", ", faltan.Fields.Select(kv => $"{kv.Key} {Unidades(kv.Value.NumberValue)}")) + ".";
            return texto;
        }

        /// <summary>Unas milésimas del contrato como unidades del juego («12» o «1,5»).</summary>
        public static string Unidades(double milesimas) => (milesimas / 1000).ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("es-ES"));

        /// <summary>El nombre de un héroe: el que trajo algún mensaje, el de su ficha si se tiene, o «Héroe N».</summary>
        public static string NombreDeHeroe(Almacen almacen, string heroeId)
        {
            if (almacen.NombresHeroes.TryGetValue(heroeId, out var n) && !string.IsNullOrEmpty(n)) return n;
            var h = almacen.Buscar(TipoEntidad.Heroe, heroeId)?.Heroe;
            if (h != null && !string.IsNullOrEmpty(h.DisplayName)) return h.DisplayName;
            const string prefijo = "heroe-";
            return heroeId.StartsWith(prefijo) && heroeId.Substring(prefijo.Length).All(char.IsDigit) ? "Héroe " + heroeId.Substring(prefijo.Length) : heroeId;
        }

        static readonly Dictionary<string, string> Mensajes = new Dictionary<string, string>
        {
            // Facción y membresía
            ["faccion.nombre_vacio"] = "La Facción necesita un nombre.",
            ["faccion.nombre_duplicado"] = "Ya existe una Facción con ese nombre: elige otro.",
            // Nombres de Facción y de loadout: 32 caracteres como mucho y sin caracteres de control (motor ef5db11).
            ["faccion.nombre_largo"] = "El nombre de la Facción tiene como mucho 32 caracteres.",
            ["faccion.nombre_control"] = "El nombre de la Facción lleva caracteres que no valen.",
            ["loadout.nombre_largo"] = "El nombre del loadout tiene como mucho 32 caracteres.",
            ["loadout.nombre_control"] = "El nombre del loadout lleva caracteres que no valen.",
            ["faccion.sigilo_invalido"] = "El sigilo no es válido: alguna pieza no es del catálogo, o el color principal y el secundario del fondo son el mismo.",
            ["faccion.sigilo_duplicado"] = "Otra Facción ya lleva exactamente ese sigilo: cambia alguna pieza o color.",
            ["faccion.ya_pertenece"] = "Ya perteneces a una Facción: tienes que dejarla antes.",
            ["faccion.cooldown_creacion"] = "Creaste o dejaste una Facción hace poco: tienes que esperar a que acabe el enfriamiento de creación.",
            ["faccion.no_pertenece"] = "No perteneces a ninguna Facción.",
            ["faccion.no_existe"] = "Esa Facción no existe.",
            ["faccion.invalida"] = "La Facción no admite esa acción ahora (comprueba si eres su Rey o su Embajador).",
            ["faccion.capital_invalida"] = "Esa plaza no puede ser la capital.",
            ["cargo.invalido"] = "No puedes asignar ese cargo: comprueba que seas Rey o Gobernador y que la persona sea ciudadana.",
            ["anexion.invalida"] = "La anexión no es posible ahora.",
            ["anexion.no_existe"] = "Esa propuesta de anexión ya no existe.",
            ["anexion.caducada"] = "La propuesta de anexión caducó.",
            ["fusion.invalida"] = "La fusión no es posible ahora.",
            ["fusion.no_existe"] = "Esa propuesta de fusión ya no existe.",
            ["fusion.caducada"] = "La propuesta de fusión caducó.",
            ["relacion.propuesta_no_existe"] = "Esa propuesta ya no existe (la retiraron o la contestaron).",
            ["relacion.propuesta_caducada"] = "La propuesta caducó.",
            ["diplomacia.invalida"] = "La diplomacia no admite esa acción ahora.",
            ["diplomacia.relacion_no_indicada"] = "Falta indicar la relación.",
            ["politica.invalida"] = "Esa política no se puede adoptar ahora.",
            // Héroe y campamento
            ["heroe.invalido"] = "El héroe no puede hacer eso (no cabe en el Liderazgo o el cupo, falta algo, o el nombre está vacío).",
            ["heroe.ya_existe"] = "Ya tienes un héroe en esta partida.",
            ["heroe.nombre_vacio"] = "Tu héroe necesita un nombre.",
            ["heroe.nombre_en_uso"] = "Ya hay un héroe con ese nombre: elige otro.",
            ["heroe.nombre_invalido"] = "El nombre del héroe debe tener de 1 a 24 caracteres, sin espacios al principio ni al final.",
            // Misiones
            ["mision.desconocida"] = "Esa misión no existe.",
            ["mision.no_cumplida"] = "Esa misión aún no está cumplida.",
            ["mision.ya_reclamada"] = "Esa recompensa ya se reclamó.",
            ["almacen_personal.lleno"] = "La recompensa no cabe entera en tu almacén personal: haz sitio y vuelve a reclamarla (no se da nada a medias).",
            ["heroe.sin_residencia"] = "Necesitas una residencia: ahí está tu almacén personal (y, en una recompensa de misión, ahí nace la tropa).",
            ["mision.almacen_lleno"] = "La recompensa no cabe entera en el almacén (de la plaza o de la capital): haz sitio y vuelve a reclamarla.",
            ["mision.sin_capital"] = "Tu Facción no tiene ninguna plaza donde dejar la recompensa.",
            ["almacen.lleno"] = "El almacén de la plaza no tiene sitio para la recompensa.",
            ["heroe.campamento_desconocido"] = "Ese campamento de mercenarios no existe.",
            ["campamento.desconocido"] = "Ese campamento de mercenarios no existe.",
            ["campamento.no_existe"] = "Ese campamento no existe.",
            ["campamento.lejos"] = "Estás demasiado lejos: hay que estar dentro o con la columna a la puerta.",
            ["campamento.proteccion"] = "Junto a un campamento de mercenarios nadie inicia un combate.",
            ["mercenarios.invalido"] = "El campamento no admite esa acción: comprueba que residas en él, que haya existencias y que tengas con qué pagar.",
            ["alijo.desconocido"] = "Ese alijo no existe.",
            ["alijo.invalido"] = "No puedes abrir ese alijo ahora: hay que estar en el sitio.",
            // Movimiento y combate
            ["jugador.sin_columna"] = "No tienes columna en el mapa: sal al mundo primero.",
            ["jugador.no_existe"] = "Ese jugador no existe.",
            ["movilizacion.invalida"] = "La columna no puede hacer eso ahora (distancia, estado o composición).",
            ["combate.invalido"] = "Ese combate no es posible ahora.",
            ["ejercito.no_existe"] = "Esa columna ya no existe.",
            ["puerta.invalida"] = "No estás a la puerta.",
            ["batalla.no_existe"] = "Esa batalla ya no existe.",
            ["batalla.bloqueado"] = "Eso está en una batalla activa y no se toca.",
            ["batalla.invalida"] = "No puedes unirte o actuar en esa batalla.",
            ["batalla.ya_asignada"] = "Esa batalla ya tiene instancia asignada.",
            // Plazas, economía y resto
            ["asentamiento.no_existe"] = "Esa plaza no existe.",
            ["caravana.no_existe"] = "Esa caravana no existe.",
            ["caravana.no_aparcada_aqui"] = "Esa caravana no está aparcada aquí.",
            ["acuerdo.no_existe"] = "Ese acuerdo no existe.",
            ["orden.no_existe"] = "Esa oferta ya no existe.",
            ["construccion.invalida"] = "No se puede construir eso ahora (nivel, cupo, recursos o cargo).",
            ["expansion.invalida"] = "No se puede expandir así.",
            ["fundacion.fuera_del_mapa"] = "No se puede fundar fuera del mapa.",
            ["fundacion.sobre_agua"] = "No se puede fundar sobre el agua: mueve tu columna a tierra firme.",
            ["fundacion.pendiente"] = "Demasiada pendiente para fundar aquí: busca un sitio más llano.",
            ["fundacion.no_transitable"] = "No se puede fundar en terreno intransitable.",
            ["mision.fuera_de_ambito"] = "Esa misión es de otro tablón: hay que residir en la plaza o ser ciudadano de la Facción.",
            ["mision.no_disponible"] = "Esa misión no está en el tablón ahora.",
            ["mision.grupal"] = "Las misiones grupales no se aceptan: cuentan solas lo de todos.",
            ["mision.pide_cargo"] = "Esa misión solo la acepta el titular de su cargo.",
            ["mision.ya_aceptada"] = "Ya aceptaste esa misión.",
            ["mision.tope_semanal"] = "Esa misión ya se aceptó todas las veces que permite esta semana.",
            ["mision.catalogo_invalido"] = "El catálogo de misiones no es válido.",
            ["tablon.desconocido"] = "Ese tablón no existe.",
            ["tablon.eleccion_invalida"] = "La elección no vale: hasta 7 diarias y 7 semanales del catálogo.",
            ["fundacion.bosque_denso"] = "No se puede fundar en bosque cerrado: busca la orilla o un claro.",
            ["fundacion.dentro_de_zona"] = "Aquí es zona de otra plaza: aléjate de ella para fundar.",
            ["fundacion.cerca_de_plaza"] = "Demasiado cerca de otra plaza: aléjate más para fundar.",
            ["fundacion.cerca_de_campamento"] = "Demasiado cerca de un campamento: aléjate más para fundar.",
            ["fundacion.cap_alcanzado"] = "Tu Facción ya tiene todas las plazas que su nivel permite.",
            ["fundacion.sin_fundadores"] = "Nadie de la columna es ciudadano de su Facción: no hay quien funde.",
            ["fundacion.sin_faccion"] = "Una columna sin Facción no funda.",
            ["fundacion.no_esta_aqui"] = "Esa Caravana de Fundación no espera dentro de este lugar.",
            ["caravana.desconocida"] = "Esa caravana ya no existe.",
            ["caravana.faccion_ajena"] = "Esa Caravana de Fundación es de otra Facción.",
            ["columna.personal_sin_caravanas"] = "Una columna personal no lleva caravanas: sal como ejército.",
            ["fundacion.invalida"] = "No se puede fundar aquí (agua, otra plaza o campamento demasiado cerca).",
            ["fundacion.sin_caravana"] = "Para fundar necesitas llevar enganchada, como titular, una Caravana de Fundación.",
            ["mercado.orden_invalida"] = "La oferta no es válida.",
            ["tropas.reclutamiento_invalido"] = "No se puede reclutar eso ahora (cupo, recursos o edificio).",
            ["comercio.caravana_invalida"] = "La caravana no admite esa orden.",
            ["comercio.trueque_invalido"] = "El trueque no es válido.",
            ["recinto.invalido"] = "La muralla no admite esa acción.",
            ["ascenso.invalido"] = "La plaza aún no puede subir de nivel.",
            ["ascenso.falta_aldea"] = "Para subir a nivel 2 la plaza necesita un pueblo activo: una aldea agrícola o un extractor.",
            ["obra.falta_edificio"] = "Falta el edificio que pide: el Albergue de caravanas se levanta junto a un Mercado activo.",
            ["obra.solo_en_aldea"] = "El Corral ya no se construye en la ciudad: va en la aldea agrícola.",
            // Aldea agrícola (motor 7086ebc)
            ["pueblo.fertilidad_insuficiente"] = "Tierra poco fértil para una aldea de ese cultivo (hace falta al menos 0,3).",
            ["pueblo.demasiado_cerca"] = "Demasiado cerca de la plaza o de otro pueblo (al menos 3).",
            ["pueblo.fuera_de_su_anillo"] = "La siguiente aldea va en su anillo de la zona: las dos primeras hasta el techo del nivel 1 (10 u), la tercera en el anillo que añade el nivel 2, y así.",
            ["obra.solo_por_pueblo"] = "Ese extractor ya no se construye en la plaza: la Cantera y las minas van en un pueblo extractor (pestaña Territorio).",
            ["faccion.de_bots"] = "Es una Facción de bots: un jugador no puede entrar en ella.",
            ["faccion.no_es_de_bots"] = "Un bot solo entra en una Facción de bots.",
            ["pueblo.fuera_de_la_zona"] = "Ese punto está fuera de la zona de la plaza o no es tierra firme.",
            ["pueblo.sin_ruta"] = "No hay camino desde la plaza hasta ese punto.",
            ["pueblo.sin_cupo"] = "La plaza ya tiene todos los pueblos que su nivel permite.",
            ["pueblo.nivel_insuficiente"] = "La plaza no tiene nivel para eso todavía (una cantera o una mina no sube por encima del nivel de su plaza).",
            ["pueblo.no_es_aldea"] = "Ese pueblo no es una aldea agrícola.",
            ["pueblo.no_esta_activo"] = "El pueblo aún no está terminado.",
            ["pueblo.mejora_en_curso"] = "Ese pueblo ya se está mejorando.",
            ["pueblo.nivel_maximo"] = "Ese pueblo ya está en su nivel máximo.",
            ["pueblo.escuadra_fuera_de_tu_columna"] = "Esa escuadra no va en la columna con la que entraste.",
            ["pueblo.escuadra_no_esta"] = "Esa escuadra no está en la guarnición de este pueblo.",
            ["pueblo.recurso_ajeno"] = "Este pueblo solo admite lo que produce (la aldea de trigo: trigo, ganado, carne y leche; las demás, su cultivo: uva, aceituna, cebada o sésamo).",
            ["pueblo.almacen_sin_sitio"] = "El almacén del pueblo está lleno.",
            ["pueblo.nada_que_cargar"] = "El pueblo no tiene de eso.",
            ["pueblo.nada_que_dejar"] = "Tu carro no lleva de eso.",
            ["pueblo.faccion_ajena"] = "Solo los ciudadanos de la Facción de la plaza madre gestionan este pueblo.",
            ["heroe.no_esta_en_el_pueblo"] = "Tienes que estar dentro del pueblo.",
            ["guarnicion.faccion_ajena"] = "Solo dejas guarnición en una plaza de tu Facción.",
            ["guarnicion.no_estas_dentro"] = "Tienes que estar dentro de la plaza para recoger esa escuadra.",
            ["escuadra.fuera_de_tu_columna"] = "Esa escuadra no va en la columna con la que entraste.",
            ["guarnicion.supera_el_cupo"] = "No cabe: superas tu cupo de Liderazgo en esta guarnición.",
            ["liderazgo.superado"] = "No cabe en tu columna: superarías tu Liderazgo.",
            ["carro.no_cabe"] = "El carro de tu columna está lleno.",
            // El carro frente al almacén de la plaza donde estás dentro.
            ["almacen.solo_ciudadanos"] = "Solo los ciudadanos de la Facción dueña sacan de este almacén.",
            ["almacen.cerrado"] = "El Gobernador ha cerrado el almacén: los ciudadanos no sacan al carro.",
            ["almacen.ni_faccion_ni_aliada"] = "Solo la Facción dueña y sus aliadas o vasallas dejan aquí lo del carro.",
            ["almacen.sin_sitio"] = "El almacén de la plaza no tiene sitio para eso.",
            ["almacen.sin_existencias"] = "No hay de eso de sobra en el almacén (el trigo no baja de la reserva).",
            ["carro.no_lleva"] = "Tu carro no lleva de eso.",
            ["heroe.sin_columna"] = "Has entrado sin columna: no traes carro.",
            // Las cinco ideas del 2026-11-10 (contrato 0.21.0)
            ["reclutamiento.solo_reponer"] = "Esta plaza ha cerrado el reclutamiento a los ciudadanos que no residen en ella: solo repones una escuadra que ya tienes aquí.",
            ["consumo.no_se_pausa"] = "Ese recurso no es un bien de consumo que se pueda pausar (solo carne, pan, queso, vino, cerveza y aceite).",
            ["rumor.sin_novedad"] = "Tu Facción ya conoce todo lo que cuenta ese rumor en esta región: no se ha cobrado nada.",
            ["intel.sin_oro"] = "No hay oro para pagarlo.",
            ["heroe.no_esta_en_la_plaza"] = "Tienes que estar dentro de la plaza.",
            ["tecnologia.adopcion_invalida"] = "No se puede adoptar esa tecnología ahora.",
            ["aedas.venta_invalida"] = "No se puede vender eso al aeda.",
            ["aedas.epica_invalida"] = "La épica no es válida.",
            ["intel.invalida"] = "La compra de intel no es posible: comprueba la taberna, el oro, el cupo de Miradas y el enfriamiento.",
            ["reserva.sin_tesorero"] = "La plaza no tiene Tesorero.",
            // Encargos de caravana (rutas y envíos automáticos, motor 2c4bca6).
            // Mercenarios por contrato (motor 7aa44b9)
            ["mercenarios.sin_oro"] = "No llevas en el carro el oro de la tarifa.",
            ["mercenario.solo_bots"] = "Solo un héroe bot se ofrece como mercenario.",
            ["mercenario.no_ofrecido"] = "Ese héroe ya no está en el tablón del campamento.",
            ["mercenario.faccion_de_bots"] = "Una Facción de bots no contrata mercenarios.",
            ["mercenario.objetivo_invalido"] = "Ese objetivo no vale para ese trabajo u orden (o ya no existe), o la residencia del permanente no es una plaza de tu Facción.",
            ["mercenario.plazo_invalido"] = "El plazo no vale: un temporal es de 1, 3 o 7 días y el permanente no lleva plazo.",
            ["mercenario.sin_alcance"] = "Ninguno de tus cargos alcanza para ese contrato, esa orden o esa valoración.",
            ["mercenario.recontrato_en_espera"] = "Tu Facción lo despidió hace poco: aún no puede volver a contratarlo.",
            ["mercenario.sin_contrato"] = "No tiene contrato vigente con tu Facción.",
            ["mercenario.tipo_de_contrato"] = "Eso no vale para su tipo de contrato (un permanente no se prorroga y un temporal no se muda).",
            ["mercenario.sin_orden"] = "Un mercenario solo hace eso con una orden que se lo permita.",
            ["mercenario.orden_fuera_de_contrato"] = "Su contrato no admite esa orden, o el objetivo queda fuera de lo contratado.",
            ["mercenario.orden_de_mas_rango"] = "Ya tiene una orden de un cargo de más rango que el tuyo.",
            ["mercenario.lista_invalida"] = "Marca entre 1 y 16 mercenarios, sin repetir.",
            ["mercenario.prohibido"] = "Un mercenario con marca no hace eso nunca.",
            ["mercenario.convocatoria_de_mercenarios"] = "A la convocatoria de un mercenario solo se unen mercenarios de su Facción con la orden de unirse a ella.",
            ["mercenario.marcado"] = "Es un mercenario con marca: no deja la Facción por su cuenta ni recibe cargos, trono ni mando.",
            ["mercenario.sin_valoracion"] = "No hay contrato terminado suyo que valorar: no lo hubo, ya se valoró o pasó el plazo.",
            ["mercenario.sin_premio"] = "No te queda ninguna escuadra de premio de rango por reclamar.",
            ["mercenario.fuera_de_licencia"] = "Esa tropa queda por encima de tu rango, o no es de tu leva propia ni la recluta tu campamento.",
            ["faccion.mercenario_ofrecido"] = "Estás en el tablón de un campamento como mercenario: retira antes tu oferta.",
            ["encargo.mismo_origen"] = "El destino no puede ser la plaza de origen de la caravana.",
            ["encargo.destino_ajeno"] = "Una ruta solo va entre plazas de tu Facción (ni aliadas): para una plaza ajena, usa un envío.",
            ["encargo.linea_invalida"] = "Alguna línea no vale: cantidad o tope de menos de 1, un recurso repetido en la misma lista, precio en una plaza tuya u oro en una ajena.",
            ["encargo.falta_precio"] = "En una plaza ajena cada línea lleva su precio, mayor que 0.",
            ["encargo.supera_capacidad"] = "Lo que recoge en una parada no cabe en la caravana.",
            ["encargo.ya_tiene"] = "Esa caravana ya tiene un encargo: cámbialo o cancélalo.",
            ["encargo.no_tiene"] = "Esa caravana no tiene encargo.",
            ["caravana.salida_invalida"] = "La hora de salida no vale: una ruta sale ya, y un envío solo se programa dentro del plazo.",
            ["caravana.no_es_comercial"] = "Solo una caravana comercial de la flota admite eso.",
            ["caravana.no_disponible"] = "No hay una caravana libre para eso: tiene que estar parada en su origen (y, para tomar una orden a distancia, con sitio para lo pedido).",
            ["caravana.sin_capacidad"] = "La caravana no tiene ningún carro con animal.",
            ["caravana.sin_ruta"] = "No hay camino por tierra hasta ese destino.",
            // Tanda 69 (motor d0326fd): monedas, prestigio, el buey del molino, el cultivo de la aldea y la taberna regional.
            ["heroe.sin_monedas"] = "No tienes las monedas que cuesta.",
            ["obra.prestigio_insuficiente"] = "Tu Facción no tiene aún el prestigio que pide esa obra.",
            ["obra.solo_en_capital"] = "La estatua solo se levanta en la capital de la Facción.",
            ["molino.no_es_molino"] = "El buey solo va en un molino.",
            ["molino.ya_tiene_buey"] = "Ese molino ya tiene su buey.",
            ["pueblo.cultivo_bloqueado"] = "Ninguna plaza de tu Facción tiene aún el nivel que pide ese cultivo: la vid y la cebada, el 3; el olivo y el sésamo, el 4.",
            ["diplomacia.tope_alianzas"] = "Una de las dos Facciones ya tiene todas las alianzas que le deja su prestigio (una, y una más cada 100).",
            ["intel.sin_informe_de_ordenes"] = "Para tomar esa orden a distancia hace falta un informe de órdenes vigente de su región.",
            ["intel.informe_reciente"] = "Ya compraste ese informe hace poco: espera a que se pueda repetir.",
            ["mercenario.fuera_de_la_region"] = "Desde la taberna solo se contrata a los mercenarios de los campamentos de su región.",
            // Cebada, sésamo y una receta por bodega y prensa (motor b55e90f).
            ["obra.tope_por_plaza"] = "La plaza ya tiene todas las bodegas (o prensas) que caben: tres.",
            ["receta.no_se_elige"] = "Solo la bodega y la prensa eligen receta.",
            ["receta.desconocida"] = "Ese edificio no tiene una receta con ese insumo.",
            ["receta.tope_por_plaza"] = "La plaza ya trabaja todas las recetas distintas que le deja su nivel en ese edificio: la bodega, una hasta el nivel 4 y las dos en el 5; la prensa, siempre una.",
            ["edificio.desconocido"] = "Ese edificio ya no está en la plaza.",
            // Servicio de acceso del motor (`POST /v1/registro` y `/v1/sesiones`), por si llega el código sin su texto.
            ["datos_invalidos"] = "Nick de 3 a 32 caracteres (letras, cifras, «_», «-» o «.») y contraseña de 8 a 128.",
            ["nick_repetido"] = "Ese nick ya está registrado.",
            ["credenciales_invalidas"] = "Nick o contraseña incorrectos.",
            ["codigo_incorrecto"] = "El código de invitación no es válido.",
            ["ocupado"] = "El servidor está ocupado: vuelve a intentarlo en unos segundos.",
            ["origen_no_permitido"] = "El servidor no acepta conexiones desde esta dirección del cliente (BRONZEAGE_ORIGENES).",
        };
    }
}

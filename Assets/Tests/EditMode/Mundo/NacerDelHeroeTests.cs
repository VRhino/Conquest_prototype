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
    }
}

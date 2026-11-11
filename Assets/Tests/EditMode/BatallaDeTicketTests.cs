using System.IO;
using BronzeAge.Contract.V1;
using ConquestTactics.Net;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// El resultado de una batalla de ticket pasa la comprobación del motor. Lee el dorado del contrato del checkout del
/// motor, al lado de este proyecto (no se copia); sin él, la prueba se salta.
/// </summary>
public class BatallaDeTicketTests
{
    static readonly string Dorado = Path.GetFullPath(Path.Combine(Application.dataPath,
        "../../BronzeAgeFase0/contratos/golden/MensajeDeServidor__batalla_ticket_de_caravana_con_dos.json"));

    static BatallaDeTicket Batalla()
    {
        if (!File.Exists(Dorado))
            Assert.Ignore($"Sin el contrato del motor en {Dorado}");
        var batalla = new BatallaDeTicket(BatallaDeTicket.Leer(Dorado)) { InicioMs = 1000 };
        Assert.That(batalla.Heroes.Count, Is.EqualTo(1));
        Assert.That(batalla.Escuadras.Count, Is.EqualTo(2), "esc-9 del héroe y esc-30 de los bandidos");
        return batalla;
    }

    [Test]
    public void AtacanteQueAniquilaSeLlevaLosCarros()
    {
        var batalla = Batalla();
        batalla.Escuadras.Find(e => e.Id == "esc-9").Muertos = 25;
        batalla.Escuadras.Find(e => e.Id == "esc-30").Muertos = 7;
        batalla.Heroes[0].Caido = true;

        var r = batalla.Resultado(LadoId.Atacante, RazonVictoria.Aniquilacion, 61000, "a1");

        Assert.That(BatallaDeTicket.Comprobar(batalla.Ticket, r), Is.Empty);
        Assert.That(r.CarrosPerdidos, Is.EquivalentTo(new[] { "car-4:0", "car-6:0", "car-6:1" }));
        Assert.That(r.PorHeroe[0].Derrotado, Is.True);
    }

    [Test]
    public void TiempoAgotadoSoloLoGanaElDefensor()
    {
        var batalla = Batalla();
        var bien = batalla.Resultado(LadoId.Defensor, RazonVictoria.TiempoAgotado, 61000, "a1");
        var mal = batalla.Resultado(LadoId.Atacante, RazonVictoria.TiempoAgotado, 61000, "a1");

        Assert.That(BatallaDeTicket.Comprobar(batalla.Ticket, bien), Is.Empty);
        Assert.That(bien.CarrosPerdidos, Is.Empty);
        Assert.That(BatallaDeTicket.Comprobar(batalla.Ticket, mal), Has.Member("tiempo agotado sin ganar el defensor"));
    }

    [Test]
    public void UnaEscuadraQueNoCierraSeRechaza()
    {
        var batalla = Batalla();
        var r = batalla.Resultado(LadoId.Defensor, RazonVictoria.Aniquilacion, 61000, "a1");
        foreach (var e in r.PorEscuadra)
            if (e.SquadId == "esc-9") e.Muertos += 1;

        Assert.That(BatallaDeTicket.Comprobar(batalla.Ticket, r), Has.Member("la escuadra esc-9 no cierra"));
    }
}

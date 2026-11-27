// La cámara libre de la batalla (pedido del autor, 2026-11-27): un botón (o Tab) despega la cámara del héroe para recorrer la batalla; otra vez,
// vuelve a él. Mientras está libre, el teclado y el ratón mueven la cámara y no al héroe ni a su escuadra (HeroInputSystem, SquadControlSystem y
// la entrada de red lo miran aquí). Solo es estado de la interfaz: el héroe sigue en el mundo ECS como estaba.
using System;

public static class CamaraLibre
{
    public static bool Activa { get; private set; }
    public static event Action<bool> AlCambiar;

    public static void Poner(bool activa)
    {
        if (Activa == activa) return;
        Activa = activa;
        AlCambiar?.Invoke(activa);
    }
}

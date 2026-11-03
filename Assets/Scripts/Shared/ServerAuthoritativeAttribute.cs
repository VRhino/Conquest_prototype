using System;

/// <summary>
/// Marks a system that decides something that changes the match and so, with a network, belongs to the server
/// (Aprendizajes_Referencia.md L4-06; ref.: the server decides what each squad sees and its candidates, and the squad AI
/// is common code both sides run). Today it only documents and is checked by an architecture test: a marked system
/// reads no input, camera, animation or other presentation, and draws no unseeded randomness, so it can later be put
/// under a server world filter without rewriting. It does not change the update order.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ServerAuthoritativeAttribute : Attribute { }

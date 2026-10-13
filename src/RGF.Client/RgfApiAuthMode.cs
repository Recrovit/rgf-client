namespace Recrovit.RecroGridFramework.Client;

/// <summary>Authentication adapter selected for the client HTTP services.</summary>
public enum RgfApiAuthMode
{
    /// <summary>No built-in token or host-session integration.</summary>
    None = 0,
    /// <summary>Browser-acquired bearer tokens for direct API access.</summary>
    WasmBearer = 1,
    /// <summary>Interactive client calls through the authenticated host origin.</summary>
    ServerProxy = 2,
    /// <summary>SSR host calls through the proxy with incoming request cookies.</summary>
    ServerProxySsr = 3,
}

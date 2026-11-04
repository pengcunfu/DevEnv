using System.Text;

namespace DevEnv.Host.SigXor.ViewModels;

public sealed record EncodingOption(string DisplayName, Encoding Encoding, bool WriteBom)
{
    public override string ToString() => DisplayName;
}

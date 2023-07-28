using ProtoBuf;

namespace Integrations.Cod.SecureRcon;

[ProtoContract]
public class SecureCommand
{
    [ProtoMember(1)] 
    public byte[] SecMessage { get; set; }
    
    [ProtoMember(2)]
    public byte[] Signature { get; set; }
}

namespace Application.Ports.Auth; 

public interface IEncryptorService
{
    string Protect(string plain);
    string UnProtect(string cipher);
}

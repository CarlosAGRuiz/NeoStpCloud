using System.Security.Cryptography;
using System.Text;
using NeoSTP.Application.Ops;

namespace NeoSTP.Infrastructure.Services;

/// <summary>
/// TOTP RFC 6238 (HMAC-SHA1, 6 dígitos, paso de 30 segundos). Sin dependencias externas.
/// </summary>
public class TotpService : ITotpService
{
    private const int Digitos = 6;
    private const int PasoSegundos = 30;
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    public string GenerarSecreto(int bytes = 20)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Base32Encode(buffer);
    }

    public string BuildOtpAuthUri(string secretBase32, string accountLabel, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountLabel}");
        var iss = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secretBase32}&issuer={iss}&algorithm=SHA1&digits={Digitos}&period={PasoSegundos}";
    }

    public bool Validar(string secretBase32, string code, int ventana = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        code = code.Trim();
        var contador = ContadorActual(DateTimeOffset.UtcNow);
        for (var i = -ventana; i <= ventana; i++)
        {
            var esperado = Calcular(secretBase32, contador + i);
            // Comparación en tiempo constante para evitar timing attacks.
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(esperado), Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    public string GenerarCodigo(string secretBase32, DateTimeOffset instante)
        => Calcular(secretBase32, ContadorActual(instante));

    private static long ContadorActual(DateTimeOffset at)
        => (long)((at - Epoch).TotalSeconds / PasoSegundos);

    private static string Calcular(string secretBase32, long contador)
    {
        var key = Base32Decode(secretBase32);
        var mensaje = new byte[8];
        var c = contador;
        for (var i = 7; i >= 0; i--)
        {
            mensaje[i] = (byte)(c & 0xff);
            c >>= 8;
        }

        var hash = HMACSHA1.HashData(key, mensaje);
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        var otp = binary % (int)Math.Pow(10, Digitos);
        return otp.ToString().PadLeft(Digitos, '0');
    }

    // -- Base32 (RFC 4648, sin padding) ----------------------------------
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length + 4) / 5 * 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1f]);
            }
        }
        if (bitsLeft > 0)
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1f]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.Trim().TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(input.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in input)
        {
            var val = Base32Alphabet.IndexOf(c);
            if (val < 0) continue; // ignora caracteres inválidos
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xff));
            }
        }
        return bytes.ToArray();
    }
}

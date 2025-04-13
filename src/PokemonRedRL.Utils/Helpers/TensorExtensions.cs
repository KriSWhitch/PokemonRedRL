using System.IO;
using TorchSharp;
using static TorchSharp.torch;

namespace PokemonRedRL.Utils.Helpers;

public static class TensorExtensions
{
    public static Tensor LoadTensorFromBytes(byte[] bytes)
    {
        // Создаем временный файл для TorchSharp
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, bytes);
            return load(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    public static byte[] SaveTensorToBytes(Tensor tensor)
    {
        // Создаем временный файл для TorchSharp
        var tempFile = Path.GetTempFileName();
        try
        {
            save(tensor, tempFile);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
using System.IO;
using TorchSharp;
using static TorchSharp.torch;

namespace Utils;

public static class TensorExtensions
{
    public static Tensor LoadTensorFromBytes(byte[] bytes)
    {
        // Создаем временный файл для TorchSharp
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, bytes);
            return torch.load(tempFile);
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
            torch.save(tensor, tempFile);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
using Parquet.Serialization.Attributes;
using static TorchSharp.torch;
using PokemonRedRL.Utils.Helpers;
using PokemonRedRL.Core.Enums;

namespace PokemonRedRL.Models.ReinforcementLearning;

public class Experience
{
    public byte[] StateBytes { get; set; }
    public ActionType Action { get; set; }
    public float Reward { get; set; }
    public byte[] NextStateBytes { get; set; }
    public bool Done { get; set; }

    [ParquetIgnore]
    public Tensor State
    {
        get => TensorExtensions.LoadTensorFromBytes(StateBytes);
        set => StateBytes = TensorExtensions.SaveTensorToBytes(value);
    }

    [ParquetIgnore]
    public Tensor NextState
    {
        get => TensorExtensions.LoadTensorFromBytes(NextStateBytes);
        set => NextStateBytes = TensorExtensions.SaveTensorToBytes(value);
    }
}
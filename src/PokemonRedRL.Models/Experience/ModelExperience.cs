using Parquet.Serialization.Attributes;
using static TorchSharp.torch;
using PokemonRedRL.Utils.Helpers;
using PokemonRedRL.Utils.Enums;
using MessagePack;
using System.Security.Cryptography;

namespace PokemonRedRL.Models.Experience;

[Serializable]
[MessagePackObject]  // <-- Required for MessagePack serialization
public class ModelExperience
{
    private Guid _id;

    [Key(0)]
    public Guid Id
    {
        get => _id;
        set => _id = value == Guid.Empty ? Guid.NewGuid() : value;
    }
    [Key(1)] 
    public float Weight { get; set; } = 1.0f;
    [Key(2)]
    public byte[] StateBytes { get; set; }

    [Key(3)]
    public ActionType Action { get; set; }

    [Key(4)]
    public float Reward { get; set; }

    [Key(5)]
    public byte[] NextStateBytes { get; set; }

    [Key(6)]
    public bool Done { get; set; }

    public ModelExperience()
    {
        _id = Guid.NewGuid(); // Гарантированная инициализация
    }

    [ParquetIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [IgnoreMember]  // <-- Tells MessagePack to skip this property
    public Tensor State
    {
        get => TensorExtensions.LoadTensorFromBytes(StateBytes);
        set => StateBytes = TensorExtensions.SaveTensorToBytes(value);
    }

    [ParquetIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [IgnoreMember]  // <-- Tells MessagePack to skip this property
    public Tensor NextState
    {
        get => TensorExtensions.LoadTensorFromBytes(NextStateBytes);
        set => NextStateBytes = TensorExtensions.SaveTensorToBytes(value);
    }
}
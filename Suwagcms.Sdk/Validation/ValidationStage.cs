namespace Suwagcms.Sdk.Validation;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ValidationStage
{
    Save,
    Version,
}

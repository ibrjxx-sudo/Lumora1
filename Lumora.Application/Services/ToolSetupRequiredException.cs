namespace Lumora.Application.Services;
/// <summary>Missing local tool data is a setup state, never a reason to launch a media picker.</summary>
public sealed class ToolSetupRequiredException(string modelId):Exception("Offline tool setup is required.")
{
    public string ModelId{get;}=modelId;
}

using System.IO;
using System.Text.Json;
using JollyFMSynth.Synthesis;

namespace JollyFMSynth.Services
{
    public static class PatchFileService
    {
        public static SynthPatch Load(string filePath) => JsonSerializer.Deserialize<SynthPatch>(File.ReadAllText(filePath), SynthPatch.JsonOptions) ?? throw new InvalidDataException("Empty patch file.");

        public static void Save(string filePath, SynthPatch patch)
        {
            patch.Name = Path.GetFileNameWithoutExtension(filePath);
            File.WriteAllText(filePath, JsonSerializer.Serialize(patch, SynthPatch.JsonOptions));
        }
    }
}
/// <summary>
/// Interface for converting other file extensions into Ragnarock maps.
/// </summary>
public interface IMapConverter
{
    /// <summary>
    /// Parses the given file and extracts relevant data into beatmap.
    /// </summary>
    void Convert(string file, RagnarockMap beatmap);
}
namespace Seq.Input.Beats
{
    public enum States
    {
        READ_HEADER = 1, //(1),
        READ_FRAME_TYPE, //(1),
        READ_WINDOW_SIZE, //(4),
        READ_JSON_HEADER, //(8),
        READ_COMPRESSED_FRAME_HEADER, // (4),
        READ_COMPRESSED_FRAME, //(-1), // -1 means the length to read is variable and defined in the frame itself.
        READ_JSON, //(-1),
        READ_DATA_FIELDS, //(-1);
    }
}

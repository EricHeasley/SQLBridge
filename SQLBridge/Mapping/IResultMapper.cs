using System.Data;

namespace SQLBridge {
    /// <summary>
    /// Turns the current row of a data reader into a <typeparamref name="T"/>. Supply your own with
    /// <see cref="ISqlRequest.MapWith{T}"/> (one request) or <see cref="SqlBridgeOptions.AddMapper{T}"/> (every request)
    /// when column-name mapping is not enough.
    /// </summary>
    /// <typeparam name="T">The mapped type.</typeparam>
    public interface IResultMapper<out T> {

        /// <summary>Maps the current row. Do not advance the record.</summary>
        /// <param name="record">The record positioned on a row.</param>
        T Map(IDataRecord record);

    }
}

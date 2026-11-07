using System.Data;

namespace SQLBridge {
    public interface IResultMapper<out T> {

        T Map(IDataRecord record);

    }
}

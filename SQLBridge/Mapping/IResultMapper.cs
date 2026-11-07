using System.Data;

namespace SQLBridge.Mapping {
    public interface IResultMapper<out T> {

        T Map(IDataRecord record);

    }
}

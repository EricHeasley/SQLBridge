using System.Data;

namespace SQLBridge.Service.Contracts.Mapping {
    public interface IResultMapper<out T> {

        T Map(IDataRecord record);

    }
}

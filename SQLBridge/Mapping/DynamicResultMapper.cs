using System.Data;
using System.Dynamic;

namespace SQLBridge.Mapping {
    internal sealed class DynamicResultMapper : IResultMapper<ExpandoObject> {

        public ExpandoObject Map(IDataRecord record) {
            var expando = new ExpandoObject() as IDictionary<string, object?>;

            for (var i = 0; i < record.FieldCount; i++) {
                var name = record.GetName(i);
                var value = record.IsDBNull(i) ? null : record.GetValue(i);
                expando[name] = value;
            }

            return (ExpandoObject)expando;
        }

    }
}

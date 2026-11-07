using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLBridge.Tests {
    public class OutputParameterCollectionTests {

        private static OutputParameterCollection Capture() {
            using var command = new SqlCommand();
            command.Parameters.Add(new SqlParameter("@Input", 5));
            command.Parameters.Add(new SqlParameter("@NewId", SqlDbType.Int) { Direction = ParameterDirection.Output, Value = 42 });
            command.Parameters.Add(new SqlParameter("@Note", SqlDbType.NVarChar, 50) { Direction = ParameterDirection.Output, Value = DBNull.Value });
            command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue, Value = 0 });

            var outputs = new OutputParameterCollection();
            outputs.Capture(command.Parameters);
            return outputs;
        }

        [Fact]
        public void Get_ReturnsTypedValueWithOrWithoutAtPrefix() {
            var outputs = Capture();

            Assert.Equal(42, outputs.Get<int>("NewId"));
            Assert.Equal(42, outputs.Get<int>("@NewId"));
        }

        [Fact]
        public void Get_ReturnsDefaultForDbNull() {
            Assert.Null(Capture().Get<string>("Note"));
        }

        [Fact]
        public void Capture_SkipsInputOnlyParameters() {
            var outputs = Capture();

            Assert.False(outputs.HasParameter("Input"));
            Assert.Throws<KeyNotFoundException>(() => outputs.Get<int>("Input"));
        }

        [Fact]
        public void ReturnValue_IsExposed() {
            Assert.Equal(0, Capture().ReturnValue);
        }

    }
}

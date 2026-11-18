using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
namespace FinanceMappingTool
{
    public sealed class ReferenceConnectionSettings
    {
        public string Server { get; set; } = "";
        public string Database { get; set; } = "";
        public bool WindowsAuthentication { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public bool Encrypt { get; set; } = true;
        public bool TrustServerCertificate { get; set; }
        public ReferenceConnectionSettings WithoutPassword()
        { var copy = (ReferenceConnectionSettings)MemberwiseClone(); copy.Password = ""; return copy; }
        public string ConnectionString()
        {
            var builder = new SqlConnectionStringBuilder { DataSource = Server.Trim(), InitialCatalog = Database.Trim(),
                IntegratedSecurity = WindowsAuthentication, ConnectTimeout = 10, Pooling = false,
                PersistSecurityInfo = false, Encrypt = Encrypt, TrustServerCertificate = TrustServerCertificate,
                ApplicationName = "FinanceMappingTool.ReferenceRead" };
            if (!WindowsAuthentication) { builder.UserID = User.Trim(); builder.Password = Password; }
            return builder.ConnectionString;
        }
    }
    public static class CustomerReferenceReader
    {
        public const string Query = "SET LOCK_TIMEOUT 10000; SELECT [FeeNo],[CustomerType] FROM [dbo].[VoucherFeeRelation] WHERE [OverseasType]=@OverseasType AND [IsDeleted]=0;";
        public static List<CustomerReference> Read(ReferenceConnectionSettings settings)
        {
            var result = new List<CustomerReference>();
            using (var connection = new SqlConnection(settings.ConnectionString()))
            using (var command = new SqlCommand(Query, connection))
            {
                command.CommandTimeout = 30;
                command.Parameters.Add("@OverseasType", SqlDbType.VarChar, 50).Value = "KDVoucher";
                connection.Open();
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        if (!reader.IsDBNull(0)) result.Add(new CustomerReference {
                            FeeNo = reader.GetString(0), CustomerType = reader.IsDBNull(1) ? (byte?)null : reader.GetByte(1) });
            }
            return result;
        }
    }
}

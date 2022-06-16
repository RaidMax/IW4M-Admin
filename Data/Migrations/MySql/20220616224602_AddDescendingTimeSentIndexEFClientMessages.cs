using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    public partial class AddDescendingTimeSentIndexEFClientMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"create index IX_EFClientMessages_TimeSentDesc on efclientmessages (TimeSent desc);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"drop index IX_EFClientMessages_TimeSentDesc on efclientmessages;");
        }
    }
}

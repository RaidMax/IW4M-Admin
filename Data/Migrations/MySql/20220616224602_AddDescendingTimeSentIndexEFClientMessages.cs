using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.MySql
{
    public partial class AddDescendingTimeSentIndexEFClientMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            try
            {
                migrationBuilder.Sql(@"create index IX_EFClientMessages_TimeSentDesc on EFClientMessages (TimeSent desc);");
            }
            catch
            {
                migrationBuilder.Sql(@"create index IX_EFClientMessages_TimeSentDesc on efclientmessages (TimeSent desc);");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"drop index IX_EFClientMessages_TimeSentDesc on EFClientMessages;");
        }
    }
}

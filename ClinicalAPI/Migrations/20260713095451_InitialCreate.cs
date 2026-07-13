using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicalAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MEDICAL_RECORDS",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PATIENT_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PATIENT_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SYMPTOMS = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    DIAGNOSIS = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    TREATMENT = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    DOCTOR_NAME = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    VISIT_DATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE"),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    NOTES = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEDICAL_RECORDS", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MEDICINES",
                columns: table => new
                {
                    ID = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TRADE_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    GENERIC_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    UNIT = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    STRENGTH = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: true),
                    CATEGORY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    MANUFACTURER = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: true),
                    COST_PRICE = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    SELL_PRICE = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    STOCK_QUANTITY = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    IS_ACTIVE = table.Column<bool>(type: "NUMBER(1)", nullable: false, defaultValue: true),
                    UPDATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false, defaultValueSql: "SYSDATE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEDICINES", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MEDICAL_RECORDS_PATIENT_ID",
                table: "MEDICAL_RECORDS",
                column: "PATIENT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEDICAL_RECORDS_STATUS",
                table: "MEDICAL_RECORDS",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "IX_MEDICINES_GENERIC_NAME",
                table: "MEDICINES",
                column: "GENERIC_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_MEDICINES_IS_ACTIVE",
                table: "MEDICINES",
                column: "IS_ACTIVE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MEDICAL_RECORDS");

            migrationBuilder.DropTable(
                name: "MEDICINES");
        }
    }
}

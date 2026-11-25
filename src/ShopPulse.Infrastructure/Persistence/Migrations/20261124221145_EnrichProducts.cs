using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ShopPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            // Piège : la séquence identity démarre à 1 alors que le seed occupe 1..200 —
            // on la cale sur max(Id)+1 (sinon : duplicate key PK_products à la 1re création).
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('products', 'Id'),
                              COALESCE((SELECT max("Id") FROM products), 0) + 1, false);
                """);

            migrationBuilder.AddColumn<string>(
                name: "Features",
                table: "products",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<decimal>(
                name: "OldPrice",
                table: "products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "products",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0001" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0002" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0003" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0004" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 21.88m, "SP-0005" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0006" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0007" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0008" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0009" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 31.25m, "SP-0010" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0011" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0012" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0013" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0014" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 40.62m, "SP-0015" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0016" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0017" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0018" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0019" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 50.00m, "SP-0020" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0021" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0022" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0023" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0024" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 59.38m, "SP-0025" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0026" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0027" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 28,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0028" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 29,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0029" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 30,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 68.75m, "SP-0030" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0031" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0032" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 33,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0033" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 34,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0034" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 35,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 78.12m, "SP-0035" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0036" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 37,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0037" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 38,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0038" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 39,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0039" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 40,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 87.50m, "SP-0040" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 41,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0041" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 42,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0042" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 43,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0043" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 44,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0044" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 45,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 96.88m, "SP-0045" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 46,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0046" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 47,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0047" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 48,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0048" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 49,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0049" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 50,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 106.25m, "SP-0050" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 51,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0051" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 52,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0052" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 53,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0053" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 54,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0054" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 55,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 115.62m, "SP-0055" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 56,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0056" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 57,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0057" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 58,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0058" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 59,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0059" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 60,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 125.00m, "SP-0060" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 61,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0061" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 62,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0062" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 63,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0063" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 64,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0064" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 65,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 134.38m, "SP-0065" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 66,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0066" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 67,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0067" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 68,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0068" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 69,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0069" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 70,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 143.75m, "SP-0070" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 71,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0071" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 72,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0072" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 73,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0073" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 74,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0074" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 75,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 153.12m, "SP-0075" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 76,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0076" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 77,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0077" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 78,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0078" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 79,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0079" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 80,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 162.50m, "SP-0080" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 81,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0081" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 82,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0082" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 83,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0083" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 84,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0084" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 85,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 171.88m, "SP-0085" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 86,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0086" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 87,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0087" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 88,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0088" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 89,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0089" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 90,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 181.25m, "SP-0090" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 91,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0091" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 92,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0092" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 93,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0093" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 94,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0094" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 95,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 190.62m, "SP-0095" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 96,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0096" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 97,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0097" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 98,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0098" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 99,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0099" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 100,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 200.00m, "SP-0100" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 101,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0101" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 102,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0102" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 103,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0103" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 104,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0104" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 105,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 209.38m, "SP-0105" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 106,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0106" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 107,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0107" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 108,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0108" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 109,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0109" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 110,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 218.75m, "SP-0110" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 111,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0111" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 112,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0112" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 113,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0113" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 114,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0114" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 115,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 228.12m, "SP-0115" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 116,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0116" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 117,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0117" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 118,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0118" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 119,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0119" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 120,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 237.50m, "SP-0120" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 121,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0121" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 122,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0122" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 123,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0123" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 124,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0124" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 125,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 246.88m, "SP-0125" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 126,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0126" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 127,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0127" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 128,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0128" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 129,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0129" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 130,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 256.25m, "SP-0130" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 131,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0131" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 132,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0132" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 133,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0133" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 134,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0134" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 135,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 265.62m, "SP-0135" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 136,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0136" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 137,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0137" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 138,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0138" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 139,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0139" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 140,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 275.00m, "SP-0140" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 141,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0141" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 142,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0142" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 143,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0143" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 144,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0144" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 145,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 284.38m, "SP-0145" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 146,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0146" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 147,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0147" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 148,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0148" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 149,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0149" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 150,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 293.75m, "SP-0150" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 151,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0151" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 152,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0152" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 153,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0153" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 154,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0154" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 155,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 303.12m, "SP-0155" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 156,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0156" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 157,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0157" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 158,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0158" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 159,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0159" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 160,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 312.50m, "SP-0160" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 161,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0161" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 162,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0162" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 163,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0163" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 164,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0164" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 165,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 321.88m, "SP-0165" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 166,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0166" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 167,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0167" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 168,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0168" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 169,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0169" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 170,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 331.25m, "SP-0170" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 171,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0171" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 172,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0172" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 173,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0173" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 174,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0174" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 175,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 340.62m, "SP-0175" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 176,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0176" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 177,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0177" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 178,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0178" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 179,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0179" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 180,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 350.00m, "SP-0180" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 181,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0181" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 182,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0182" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 183,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0183" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 184,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0184" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 185,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 359.38m, "SP-0185" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 186,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0186" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 187,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0187" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 188,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0188" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 189,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0189" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 190,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 368.75m, "SP-0190" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 191,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0191" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 192,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0192" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 193,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0193" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 194,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0194" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 195,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", 378.12m, "SP-0195" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 196,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0196" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 197,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", null, "SP-0197" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 198,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Dalle IPS, angles de vision 178\\u00B0/178\\u00B0\",\"Luminosit\\u00E9 250 cd/m\\u00B2, contraste 3000:1\",\"Technologies anti-scintillement et r\\u00E9duction de lumi\\u00E8re bleue\",\"Garantie 2 ans\"]", null, "SP-0198" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 199,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0, entr\\u00E9e AUX\",\"Autonomie jusqu\\u0027\\u00E0 30 heures d\\u0027\\u00E9coute\",\"Coussinets \\u00E0 m\\u00E9moire de forme ultra-confortables\",\"Garantie 2 ans\"]", null, "SP-0199" });

            migrationBuilder.UpdateData(
                table: "products",
                keyColumn: "Id",
                keyValue: 200,
                columns: new[] { "Features", "OldPrice", "Sku" },
                values: new object[] { "[\"Connectivit\\u00E9 Bluetooth 5.0 multi-appareils et USB-C\",\"Autonomie longue dur\\u00E9e (jusqu\\u0027\\u00E0 6 mois)\",\"Touches silencieuses \\u00E0 faible course\",\"Garantie 2 ans\"]", 387.50m, "SP-0200" });

            migrationBuilder.CreateIndex(
                name: "IX_products_Sku",
                table: "products",
                column: "Sku",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_Sku",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Features",
                table: "products");

            migrationBuilder.DropColumn(
                name: "OldPrice",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "products");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "products",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}

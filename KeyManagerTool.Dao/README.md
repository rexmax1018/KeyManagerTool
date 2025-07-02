# ��Ʈw (.mdf) �٭���n

����󻡩��p��ϥ� Entity Framework Core Migrations �Ӻ޲z�M�٭�M�פ�����Ʈw (.mdf) �ɮסC

## ��Ʈw�ɮ׸��|

���M�ת���Ʈw�ɮ� `KeyManagerDb.mdf` �w���|�s�b��H�U���|�]�ھ� `KeyManagerTool/appsettings.json` �����s�u�r��]�w�^�G

`E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf`

## �٭��Ʈw (.mdf �ɮ�) ���B�J

�p�G�z�ݭn���s�إߩΧ�s�W�z���|����Ʈw�ɮסA�Ш̷ӥH�U�B�J�ާ@�G

1.  **�}�� Visual Studio �����}�o�H�� PowerShell**
    �b Visual Studio ���A�̧��I�� `�u�� (Tools)` -> `�R�O�C (Command Line)` -> `�}�o�H�� PowerShell (Developer PowerShell)`�C

2.  **�ɯ��ѨM��׮ڥؿ�**
    �b�u�}�o�H�� PowerShell�v�������A�ɯ��z���ѨM��׮ڥؿ� (�q�`�O�]�t `.sln` �ɮת���Ƨ�)�C

3.  **�T�O `dotnet-ef` �u��w�w��**
    �p�G�z�|���w�� EF Core �R�O�C�u��A�Х��w�ˡG
    ```powershell
    dotnet tool install --global dotnet-ef --version 7.* # �T�O�����P�z�� EF Core �M��ǰt (e.g., 7.x)
    ```
    �p�G�w�w�˦����������T�A�i�H���Ѱ��w�˦A�w�ˡG
    ```powershell
    dotnet tool uninstall --global dotnet-ef
    dotnet tool install --global dotnet-ef --version 7.*
    ```
    �w�˩Χ�s��A�аȥ� **���s�Ұʱz�� PowerShell ����**�C

4.  **�s�W�E�� (Add-Migration)**
    �p�G�z�� `KeyManagerTool.Dao/Models/Customer.cs` �ҫ��Ψ�L��Ʈw���c�i��F���A�z�ݭn�s�W�@�ӷs���E���ӰO���o�ǧ��G
    ```powershell
    dotnet ef migrations add YourMigrationName --project KeyManagerTool.Dao --startup-project KeyManagerTool
    ```
    �Ҧp�A�z�i�H�N `YourMigrationName` �������y�z������諸�W�١A�p `AddCustomerAddress`�C�o�N�|���ͤ@�ӷs���E���ɮצb `Migrations` ��Ƨ����C

5.  **��s��Ʈw (Update-Database)**
    �o�O��ګإߩΧ�s `.mdf` �ɮת�����B�J�C���榹���O��AEntity Framework Core �|�ˬd�z���E���O���A�ñN��Ʈw��s��̷s�����A�C
    ```powershell
    dotnet ef database update --project KeyManagerTool.Dao --startup-project KeyManagerTool
    ```
    ���榹���O��G
    * �p�G `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf` �ɮפ��s�b�AEntity Framework Core �|���զb�����|�إߤ@�ӷs����Ʈw�ɮסC
    * �p�G�ɮצs�b�A���|���ΩҦ��|�����檺�E���A�ϸ�Ʈw���c�P�z���{���X�ҫ��O���@�P�C

## �`�����D�P���~�ư� (EF Core)

### ���~�G`Unable to create an object of type 'AppDbContext'.`

�o�ӿ��~��� EF Core �]�p�ɤu��L�k�إ߱z�� `AppDbContext` ��ҡC�o�q�`�O�]�� `DbContext` ���غc�l�ݭn�Ѽơ]�Ҧp `DbContextOptions<AppDbContext>`�^�A�ӳ]�p�ɤu�㤣���D�p�󴣨ѡC

**�ѨM���**�G
�b `KeyManagerTool.Dao` �M�פ���@ `IDesignTimeDbContextFactory<AppDbContext>` �����C�z�ݭn�إߤ@�����O (�Ҧp `AppDbContextFactory.cs`)�A���e������H�U�d�ҡG

```csharp
// KeyManagerTool.Dao/AppDbContextFactory.cs (�d��)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace KeyManagerTool.Dao
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
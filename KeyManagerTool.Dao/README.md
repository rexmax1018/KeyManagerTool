# ��Ʈw (.mdf) �٭���n

����󻡩��p��ϥ� Entity Framework Code First Migrations �Ӻ޲z�M�٭�M�פ�����Ʈw (.mdf) �ɮסC

## ��Ʈw�ɮ׸��|

���M�ת���Ʈw�ɮ� `KeyManagerDb.mdf` �w���|�s�b��H�U���|�]�ھ� `appsettings.json` �M `KeyManagerTool.Dao/App.config` �����s�u�r��]�w�^�G

`E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf`

## �٭��Ʈw (.mdf �ɮ�) ���B�J

�p�G�z�ݭn���s�إߩΧ�s�W�z���|����Ʈw�ɮסA�Ш̷ӥH�U�B�J�ާ@�G

1.  **�}�� Visual Studio �����M��޲z���D���x (Package Manager Console)**
    �b Visual Studio ���A�̧��I�� `�u�� (Tools)` -> `NuGet �M��޲z�� (NuGet Package Manager)` -> `�M��޲z���D���x (Package Manager Console)`�C

2.  **�]�w�w�]�M�� (Set Default Project)**
    �b�u�M��޲z���D���x�v�������A�T�{ **�w�]�M�� (Default project)** �U�Կ��w�]�w�� `KeyManagerTool.Dao`�C�o�O�]����Ʈw�E�����������O�ݭn�b�]�t `DbContext` �M Migrations �ɮת��M�פ�����C

3.  **�ҥξE�� (Enable Migrations) (�p�G�|���ҥ�)**
    �p�G�o�O�z�Ĥ@���]�w�E���A�Ϊ̤��T�w�O�_�w�ҥΡA�а���H�U���O�G
    ```powershell
    Enable-Migrations
    ```
    *�`�N�G���M�פw�]�t�E���ɮסA���B�J�q�`�|�Q���L�C*

4.  **�s�W�E�� (Add-Migration) (�p�G��Ʈw�ҫ����ܧ�)**
    �p�G�z�� `KeyManagerTool.Dao/Models/Customer.cs` �ҫ��Ψ�L��Ʈw���c�i��F���A�z�ݭn�s�W�@�ӷs���E���ӰO���o�ǧ��G
    ```powershell
    Add-Migration YourMigrationName
    ```
    �Ҧp�A�z�i�H�N `YourMigrationName` �������y�z������諸�W�١A�p `AddCustomerAddress`�C�o�N�|���ͤ@�ӷs���E���ɮצb `Migrations` ��Ƨ����C

5.  **��s��Ʈw (Update-Database)**
    �o�O��ګإߩΧ�s `.mdf` �ɮת�����B�J�C���榹���O��AEntity Framework �|�ˬd�z���E���O���A�ñN��Ʈw��s��̷s�����A�C
    ```powershell
    Update-Database
    ```
    ���榹���O��G
    * �p�G `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\KeyManagerDb.mdf` �ɮפ��s�b�AEntity Framework �|���զb�����|�إߤ@�ӷs����Ʈw�ɮסC
    * �p�G�ɮצs�b�A���|���ΩҦ��|�����檺�E���A�ϸ�Ʈw���c�P�z���{���X�ҫ��O���@�P�C

## �`�����D�P���~�ư�

### ���~�G`Cannot attach the file '...' as database '...'`

�o�ӿ��~�q�`��� SQL Server (LocalDB) �L�k�N���w�� `.mdf` �ɮת��[����Ʈw�C�o�i��O�ѩ��ɮ׳Q���ΡB�v�������� LocalDB ���������D�C

�Ш̷ӥH�U���ǹ��ճo�ǸѨM��סG

1.  **�T�O�S����L�{�������ɮסG**
    * **���s�Ұ� Visual Studio�G** �o�O�̱`���B���Ī��ѨM��סC�����Ҧ� Visual Studio ��ҡA�M�᭫�s�ҰʡC
    * **�ˬd SQL Server Management Studio (SSMS) �� Azure Data Studio�G** �p�G�z���}�ҳo�Ǥu��ós����Ӹ�Ʈw�A���_�}�s���������o�Ǥu��C
    * **�ˬd�u�@�޲z���G** �T�O�S�� `sqlservr.exe` �� `SqlLocalDB.exe` �������i�{���b�B��A�Ϊ̪������s�Ұʹq���C
    * **�������ê� `~` �{���ɮסG** ���ɷ|���@�����ê��{���ɮס]�Ҧp `~KeyManagerDb.mdf`�^��w��Ʈw�A�T�O�o���ɮפ]�Q�R���C

2.  **�ˬd�ɮ��v���G**
    * �ɯ�� `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\` ��Ƨ��C
    * �k���I�� `KeyManagerDb.mdf` (�p�G�s�b) �M `KeyManagerDb_log.ldf` (�p�G�s�b) �ɮסA��� `���e (Properties)`�C
    * �i�J `�w���� (Security)` �ﶵ�d�C
    * �T�O `USERS`�B`SYSTEM` �αz���b��㦳 **�������� (Full Control)** ���v���C�p�G�S���A���I�� `�s�� (Edit)` �òK�[�έק��v���C

3.  **�����R���{����Ʈw�ɮרí��s�إߡG**
    �p�G�W�z��k�L�ġA�̪�������k�O�� Entity Framework ���s�إߥ��s����Ʈw�ɮסC
    * **���� Visual Studio�C**
    * �ɯ�� `E:\Qsync\source\repos\CryptoMarket\KeyManagerTool\Data\` ��Ƨ��C
    * **�R��** `KeyManagerDb.mdf` �M `KeyManagerDb_log.ldf` �o����ɮ� (�p�G����
$connStr = 'Server=(localdb)\mssqllocaldb;Database=aspnet-JudicialStatistics_Web-ac023dbe-f27e-451a-b70e-4e40c8f4da4d;Trusted_Connection=True;MultipleActiveResultSets=true';
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr);
$conn.Open();

$cmd = $conn.CreateCommand();
$cmd.CommandText = @"
SELECT 
    ct.Code AS CaseTypeCode,
    ct.NameAr AS CaseTypeName,
    inc.Id AS IncomingTypeId,
    inc.Code AS IncomingCode,
    inc.NameAr AS IncomingName,
    inc.InputSource,
    r.IsRequired,
    r.SortOrder
FROM CaseTypeInputRules r
INNER JOIN CaseTypes ct ON r.CaseTypeId = ct.Id
INNER JOIN IncomingTypes inc ON r.IncomingTypeId = inc.Id
ORDER BY ct.Id, r.SortOrder
"@;

$adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd);
$table = New-Object System.Data.DataTable;
$adapter.Fill($table) | Out-Null;
$table | Format-Table -AutoSize | Out-String -Width 400 | Write-Output;

$conn.Close();

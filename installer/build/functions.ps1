function Exit-WithError
{
    param ( [string] $Message )

    Write-Host $Message
    Write-Host "Press any key to continue..."
    [System.Console]::ReadKey($true) | Out-Null
    exit 1
}

function Publish-Project
{
    param ( [string] $Source, [string] $Target, [string[]] $Parameters )

    $name = Split-Path -Path $Source -Leaf
    $project = "$Source\$name.csproj"

    Write-Host "Publishing project '$name'..."

    # $Parameters = @('-p:PublishProfile=Alpha', '-o', $TargetDirectory)

    $publishResult = dotnet publish $project @Parameters -o $Target /v:minimal 2>&1        

    if ($LASTEXITCODE -eq 0)
    {
        #Write-Host "✅ Publish succeeded for $name"
    }
    else
    {
        $publishResult | Write-Output

        Exit-WithError -Message "❌ Publish failed for $name"
    }
}
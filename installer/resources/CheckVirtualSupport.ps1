$hyperv = Get-WindowsOptionalFeature -FeatureName Microsoft-Hyper-V-All -Online

if($hyperv.State -eq "Enabled") # Check if Hyper-V is enabled
{
    "HyperV"
} 
else 
{
    ""
}
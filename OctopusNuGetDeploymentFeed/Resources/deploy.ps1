#Requires -Version 5
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Test-IsNominatedDeployer {
	$taskId = [int]$OctopusParameters['Octopus.Task.Id'].Split('-')[1]
	Write-Verbose "Octopus.Task.Id: $taskId"
	$deploymentId = [int]$OctopusParameters['Octopus.Deployment.Id'].Split('-')[1]
	Write-Verbose "Octopus.Deployment.Id: $deploymentId"
	$creationTime = [datetime]::Parse($OctopusParameters['Octopus.Deployment.CreatedUtc']).TimeOfDay.TotalSeconds
	Write-Verbose "Creation Time: $creationTime"

	$randomSeed = $taskId + $deploymentId + $creationTime
	Write-Verbose "Random Seed: $randomSeed"
	$rng = [System.Random]::new($randomSeed)
	$actionNumber = [int]$OctopusParameters['Octopus.Action.Number']
	Write-Verbose "Octopus.Action.Number: $actionNumber"
	1..$actionNumber | % { $rng.Next() } | Out-Null
	$machineSet = @($OctopusParameters['Octopus.Action.TargetRoles'] -split ',' | % { $OctopusParameters[('Octopus.Environment.MachinesInRole[{0}]' -f $_)] -split ',' } | Sort-Object -Unique)
	Write-Verbose "Machine Set: $($machineSet -join ', ')"
	$targetMachineId = $machineSet[$rng.Next() % $machineSet.Count]
	Write-Verbose "Target Machine Id: $targetMachineId"
	Write-Verbose "This Machine Id: $($OctopusParameters['Octopus.Machine.Id'])"
	
	$machineIsNominated = $targetMachineId -ieq $OctopusParameters['Octopus.Machine.Id']
	Write-Verbose "Machine Is Nominated Deployer: $machineIsNominated"
	
	return $machineIsNominated
}

function Get-DeployConfigSetting {
    param([Parameter(Position = 0, Mandatory)][string]$Name, [Parameter(Position = 1, Mandatory)]$DefaultValue, [Parameter(Position = 2)]$RuntimeDefaultValue)

	$deployConfig = [xml](Get-Content -Path (Join-Path $PSScriptRoot 'deploy.config'))
	$value = $deployConfig.configuration.appSettings.add | ? key -eq $Name | % value | % Trim
	if ($value -ieq $DefaultValue) {
		if (Test-String $RuntimeDefaultValue) {
			if ($OctopusParameters.ContainsKey($RuntimeDefaultValue)) {
				Write-Verbose "Deploy Config Setting (Runtime-Found) $Name = $($OctopusParameters[$RuntimeDefaultValue])"
				return $OctopusParameters[$RuntimeDefaultValue]
			} else {
				Write-Verbose "Deploy Config Setting (Runtime-NotFound) $Name = `$null"
				return $null
			}
		} else {
			Write-Verbose "Deploy Config Setting (Default) $Name = $($DefaultValue)"
			return $DefaultValue
		}
	} else {
		Write-Verbose "Deploy Config Setting (Config) $Name = $($value)"
		return $value
	}
}
$NuGetPackageServer = Get-Content -Path (Join-Path $PSScriptRoot 'server.json') | ConvertFrom-Json
$Chain_BaseUrl = $NuGetPackageServer.BaseUri
$Chain_ApiKey = $NuGetPackageServer.ApiKey

function Test-String {
    param([Parameter(Position=0)]$InputObject,[switch]$ForAbsence)

    $hasNoValue = [System.String]::IsNullOrWhiteSpace($InputObject)
    if ($ForAbsence) { $hasNoValue }
    else { -not $hasNoValue }
}

function Get-OctopusSetting {
    param([Parameter(Position = 0, Mandatory)][string]$Name, [Parameter(Position = 1, Mandatory)]$DefaultValue)
    $formattedName = 'Octopus.Action.{0}' -f $Name
    if ($OctopusParameters.ContainsKey($formattedName)) {
        $value = $OctopusParameters[$formattedName]
        if ($DefaultValue -is [int]) { return ([int]::Parse($value)) }
        if ($DefaultValue -is [bool]) { return ([System.Convert]::ToBoolean($value)) }
        if ($DefaultValue -is [array] -or $DefaultValue -is [hashtable] -or $DefaultValue -is [pscustomobject]) { return (ConvertFrom-Json -InputObject $value) }
        return $value
    }
    else { return $DefaultValue }
}

# Write functions are re-defined using octopus service messages to preserve formatting of log messages received from the chained deployment and avoid errors being twice wrapped in an ErrorRecord
function Write-Fatal($message, $exitCode = -1) {
    if (Test-Path Function:\Fail-Step) {
        Fail-Step $message
    }
    else {
        Write-Host ("##octopus[stdout-error]`n{0}" -f $message)
        Exit $exitCode
    }
}
function Write-Error($message) { Write-Host ("##octopus[stdout-error]`n{0}`n##octopus[stdout-default]" -f $message) }
function Write-Warning($message) { Write-Host ("##octopus[stdout-warning]`n{0}`n##octopus[stdout-default]" -f $message) }
function Write-Verbose($message) { Write-Host ("##octopus[stdout-verbose]`n{0}`n##octopus[stdout-default]" -f $message) }

$DebugLogging = Get-OctopusSetting DebugLogging $false

function Invoke-OctopusApi {
    param(
        [Parameter(Position = 0, Mandatory)]$Uri,
        [ValidateSet('Get', 'Post', 'Put')]$Method = 'Get',
        $Body,
        [switch]$GetErrorResponse
    )
    $Uri = $Uri -replace '{.*?}',''
    $requestParameters = @{
        Uri = ('{0}/{1}' -f $Chain_BaseUrl, $Uri.TrimStart('/'))
        Method = $Method
        Headers = @{ 'X-Octopus-ApiKey' = $Chain_ApiKey }
        UseBasicParsing = $true
    }
    if ($Method -ne 'Get' -or $DebugLogging) {
        Write-Verbose ('{0} {1}' -f $Method.ToUpperInvariant(), $requestParameters.Uri)
    }
    if ($null -ne $Body) {
        $requestParameters.Add('Body', (ConvertTo-Json -InputObject $Body -Depth 10))
        Write-Verbose $requestParameters.Body
    }
    
    $wait = 0
    $webRequest = $null
    while ($null -eq $webRequest) {	
        try {
            $webRequest = Invoke-WebRequest @requestParameters
        } catch {
            if ($_.Exception -is [System.Net.WebException] -and $null -ne $_.Exception.Response) {
                $errorResponse = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream()).ReadToEnd()
                Write-Verbose ("Error Response:`n{0}" -f $errorResponse)
                if ($GetErrorResponse) {
                    return ($errorResponse | ConvertFrom-Json)
                }
                if ($_.Exception.Response.StatusCode -in @([System.Net.HttpStatusCode]::NotFound, [System.Net.HttpStatusCode]::InternalServerError, [System.Net.HttpStatusCode]::BadRequest, [System.Net.HttpStatusCode]::Unauthorized)) {
                    Write-Fatal $_.Exception.Message
                }
            }
            if ($wait -eq 120) {
                Write-Fatal ("Octopus web request ({0}: {1}) failed & the maximum number of retries has been exceeded:`n{2}" -f $Method.ToUpperInvariant(), $requestParameters.Uri, $_.Exception.Message) -43
            }
            $wait = switch ($wait) {
                0 { 30 }
                30 { 60 }
                60 { 120 }
            }
            Write-Warning ("Octopus web request ({0}: {1}) failed & will be retried in $wait seconds:`n{2}" -f $Method.ToUpperInvariant(), $requestParameters.Uri, $_.Exception.Message)
            Start-Sleep -Seconds $wait
        }
    }
    $webRequest.Content | ConvertFrom-Json | Write-Output
}

enum GuidedFailure {
    Default
    Enabled
    Disabled
    RetryIgnore
    RetryAbort
    Ignore
    RetryDeployment
}

class DeploymentContext {
    hidden $BaseUrl

    DeploymentContext($baseUrl) {
        $this.BaseUrl = $baseUrl
    }

    hidden $Project
    hidden $Lifecycle
    [void] SetProject() {
        $this.Project = Get-Content -Path (Join-Path $PSScriptRoot 'project.json') | ConvertFrom-Json
        Write-Host "Project: $($this.Project.Name)"
        Write-Verbose "`t$($this.BaseUrl)$($this.Project.Links.Self)"
        
        $this.Lifecycle = Invoke-OctopusApi ('/api/lifecycles/{0}' -f $this.Project.LifecycleId)
        Write-Host "Project Lifecycle: $($this.Lifecycle.Name)"
        Write-Verbose "`t$($this.BaseUrl)$($this.Lifecycle.Links.Self)"
    }
    
    hidden $Channel
    [void] SetChannel() {
        $this.Channel = Get-Content -Path (Join-Path $PSScriptRoot 'channel.json') | ConvertFrom-Json
        Write-Host "Channel: $($this.Channel.Name)"
        Write-Verbose "`t$($this.BaseUrl)$($this.Channel.Links.Self)"

        if ($null -ne $this.Channel.LifecycleId) {
            $this.Lifecycle = Invoke-OctopusApi ('/api/lifecycles/{0}' -f $this.Channel.LifecycleId)
            Write-Host "Channel Lifecycle: $($this.Lifecycle.Name)"
            Write-Verbose "`t$($this.BaseUrl)$($this.Lifecycle.Links.Self)"        
        }
    }

    hidden $Release
    [void] SetRelease() {
        $this.Release = Get-Content -Path (Join-Path $PSScriptRoot 'release.json') | ConvertFrom-Json
        Write-Host "Release: $($this.Release.Version)"
        Write-Verbose "`t$($this.BaseUrl)/api/releases/$($this.Release.Id)"
    }

    [void] UpdateVariableSnapshot() {
        $this.Release = Invoke-OctopusApi $this.Release.Links.SnapshotVariables -Method Post
        Write-Host 'Variables snapshot update performed. The release now references the latest variables.'
    }

    hidden $DeploymentTemplate
    [void] GetDeploymentTemplate() {
        Write-Host 'Getting deployment template for release...'
        $this.DeploymentTemplate = Invoke-OctopusApi $this.Release.Links.DeploymentTemplate
    }

    hidden [bool]$UseGuidedFailure
    hidden [string[]]$GuidedFailureActions
    hidden [string]$GuidedFailureMessage
    hidden [int]$DeploymentRetryCount
    [void] SetGuidedFailure([GuidedFailure]$guidedFailure, $guidedFailureMessage) {
        $this.UseGuidedFailure = switch ($guidedFailure) {
            ([GuidedFailure]::Default) { [System.Convert]::ToBoolean($global:OctopusUseGuidedFailure) }
            ([GuidedFailure]::Enabled) { $true }
            ([GuidedFailure]::Disabled) { $false }
            ([GuidedFailure]::RetryIgnore) { $true }
            ([GuidedFailure]::RetryAbort) { $true }
            ([GuidedFailure]::Ignore) { $true } 
            ([GuidedFailure]::RetryDeployment) { $false }
        }
        Write-Host "Setting Guided Failure: $($this.UseGuidedFailure)"
        
        $retryActions = @(1..(Get-OctopusSetting StepRetryCount 1) | % {'Retry'})
        $this.GuidedFailureActions = switch ($guidedFailure) {
            ([GuidedFailure]::Default) { $null }
            ([GuidedFailure]::Enabled) { $null }
            ([GuidedFailure]::Disabled) { $null }
            ([GuidedFailure]::RetryIgnore) { $retryActions + @('Ignore') }
            ([GuidedFailure]::RetryAbort) { $retryActions + @('Abort') }
            ([GuidedFailure]::Ignore) { @('Ignore') }
            ([GuidedFailure]::RetryDeployment) { $null }
        }
        if ($null -ne $this.GuidedFailureActions) {
            Write-Host "Automated Failure Guidance: $($this.GuidedFailureActions -join '; ') "
        }
        $this.GuidedFailureMessage = $guidedFailureMessage
        
        $defaultRetries = if ($guidedFailure -eq [GuidedFailure]::RetryDeployment) { 1 } else { 0 }
        $this.DeploymentRetryCount = Get-OctopusSetting DeploymentRetryCount $defaultRetries
        if ($this.DeploymentRetryCount -ne 0) {
            Write-Host "Failed Deployments will be retried #$($this.DeploymentRetryCount) times"
        }
    }
        
    [bool]$WaitForDeployment
    hidden [datetime]$QueueTime
    hidden [datetime]$QueueTimeExpiry
    [void] SetSchedule($deploySchedule) {
        if (Test-String $deploySchedule -ForAbsence) {
            Write-Fatal 'The deployment schedule step parameter was not found.'
        }
        if ($deploySchedule -eq 'WaitForDeployment') {
            $this.WaitForDeployment = $true
            Write-Host 'Deployment will be queued to start immediatley...'
            return
        }
        $this.WaitForDeployment = $false
        if ($deploySchedule -eq 'NoWait') {
            Write-Host 'Deployment will be queued to start immediatley...'
            return
        }
        <#
            ^(?i) - Case-insensitive matching
            (?:
                (?<Day>MON|TUE|WED|THU|FRI|SAT|SUN)? - Capture an optional day
                \s*@\s* - '@' indicates deploying at a specific time
                (?<TimeOfDay>(?:[01]?[0-9]|2[0-3]):[0-5][0-9]) - Captures the time of day, in 24 hour format
            )? - Day & TimeOfDay are optional
            \s*
            (?:
                \+\s* - '+' indicates deploying after a length of tie
                (?<TimeSpan>
                    \d{1,3} - Match 1 to 3 digits
                    (?::[0-5][0-9])? - Optionally match a colon and 00 to 59, this denotes if the previous 1-3 digits are hours or minutes
                )
            )?$ - TimeSpan is optional
        #>
        $parsedSchedule = [regex]::Match($deploySchedule, '^(?i)(?:(?<Day>MON|TUE|WED|THU|FRI|SAT|SUN)?\s*@\s*(?<TimeOfDay>(?:[01]?[0-9]|2[0-3]):[0-5][0-9]))?\s*(?:\+\s*(?<TimeSpan>\d{1,3}(?::[0-5][0-9])?))?$')
        if (!$parsedSchedule.Success) {
            Write-Fatal "The deployment schedule step parameter contains an invalid value. Valid values are 'WaitForDeployment', 'NoWait' or a schedule in the format '[[DayOfWeek] @ HH:mm] [+ <MMM|HHH:MM>]'" 
        }
        $this.QueueTime = Get-Date
        if ($parsedSchedule.Groups['Day'].Success) {
            Write-Verbose "Parsed Day: $($parsedSchedule.Groups['Day'].Value)"
            while (!$this.QueueTime.DayOfWeek.ToString().StartsWith($parsedSchedule.Groups['Day'].Value)) {
                $this.QueueTime = $this.QueueTime.AddDays(1)
            }
        }
        if ($parsedSchedule.Groups['TimeOfDay'].Success) {
            Write-Verbose "Parsed Time Of Day: $($parsedSchedule.Groups['TimeOfDay'].Value)"
            $timeOfDay = [datetime]::ParseExact($parsedSchedule.Groups['TimeOfDay'].Value, 'HH:mm', $null)
            $this.QueueTime = $this.QueueTime.Date + $timeOfDay.TimeOfDay
        }
        if ($parsedSchedule.Groups['TimeSpan'].Success) {
            Write-Verbose "Parsed Time Span: $($parsedSchedule.Groups['TimeSpan'].Value)"
            $timeSpan = $parsedSchedule.Groups['TimeSpan'].Value.Split(':')
            $hoursToAdd = if ($timeSpan.Length -eq 2) {$timeSpan[0]} else {0}
            $minutesToAdd = if ($timeSpan.Length -eq 2) {$timeSpan[1]} else {$timeSpan[0]}
            $this.QueueTime = $this.QueueTime.Add((New-TimeSpan -Hours $hoursToAdd -Minutes $minutesToAdd))
        }
        Write-Host "Deployment will be queued to start at: $($this.QueueTime.ToLongDateString()) $($this.QueueTime.ToLongTimeString())"
        Write-Verbose "Local Time: $($this.QueueTime.ToLocalTime().ToString('r'))"
        Write-Verbose "Universal Time: $($this.QueueTime.ToUniversalTime().ToString('o'))"
        $this.QueueTimeExpiry = $this.QueueTime.Add([timespan]::ParseExact((Get-OctopusSetting QueueTimeout '00:30'), "hh\:mm", $null))
        Write-Verbose "Queued deployment will expire on: $($this.QueueTimeExpiry.ToUniversalTime().ToString('o'))"
    }

    hidden $Environments
    [void] SetEnvironment($environmentName) {
        $lifecyclePhaseEnvironments = $this.Lifecycle.Phases | ? Name -eq $environmentName | % {
            $_.AutomaticDeploymentTargets
            $_.OptionalDeploymentTargets
        }
        $this.Environments = $this.DeploymentTemplate.PromoteTo | ? { $_.Id -in $lifecyclePhaseEnvironments -or $_.Name -ieq $environmentName }
        if ($null -eq $this.Environments) {
            Write-Fatal "The specified environment ($environmentName) was not found or not eligible for deployment of the release ($($this.Release.Version)). Verify that the release has been deployed to all required environments before it can be promoted to this environment. Once you have corrected these problems you can try again." 
        }
        Write-Host "Environments: $(($this.Environments | % Name) -join ', ')"
    }
    
    [bool] $IsTenanted
    hidden $Tenants
    [void] SetTenants($tenantFilter) {
        $this.IsTenanted = Test-String $tenantFilter
        if (!$this.IsTenanted) {
            return
        }
        $tenantPromotions = $this.DeploymentTemplate.TenantPromotions | % Id
        $this.Tenants = $tenantFilter.Split("`n") | % { [uri]::EscapeUriString($_.Trim()) } | % {
            $criteria = if ($_ -like '*/*') { 'tags' } else { 'name' }
            
            $tenantResults = Invoke-OctopusApi ('/api/tenants/all?projectId={0}&{1}={2}' -f $this.Project.Id, $criteria, $_) -GetErrorResponse
            if ($tenantResults -isnot [array] -and $tenantResults.ErrorMessage) {
                Write-Warning "Full Exception: $($tenantResults.FullException)"
                Write-Fatal $tenantResults.ErrorMessage
            }
            $tenantResults
        } | ? Id -in $tenantPromotions

        if ($null -eq $this.Tenants) {
            Write-Fatal "No eligible tenants found for deployment of the release ($($this.Release.Version)). Verify that the tenants have been associated with the project."
        }
        Write-Host "Tenants: $(($this.Tenants | % Name) -join ', ')"
    }

    [DeploymentController[]] GetDeploymentControllers() {
        Write-Verbose 'Determining eligible environments & tenants. Retrieving deployment previews...'
        $deploymentControllers = @()
        foreach ($environment in $this.Environments) {
            $envPrefix = if ($this.Environments.Count -gt 1) {$environment.Name}
            if ($this.IsTenanted) {
                foreach ($tenant in $this.Tenants) {
                    $tenantPrefix = if ($this.Tenants.Count -gt 1) {$tenant.Name}
                    if ($this.DeploymentTemplate.TenantPromotions | ? Id -eq $tenant.Id | % PromoteTo | ? Id -eq $environment.Id) {
                        $logPrefix = ($envPrefix,$tenantPrefix | ? { $null -ne $_ }) -join '::'
                        $deploymentControllers += [DeploymentController]::new($this, $logPrefix, $environment, $tenant)
                    }
                }
            }
            else {
                $deploymentControllers += [DeploymentController]::new($this, $envPrefix, $environment, $null)
            }
        }
        return $deploymentControllers
    }
}

class DeploymentController {
    hidden [string]$BaseUrl
    hidden [DeploymentContext]$DeploymentContext
    hidden [string]$LogPrefix
    hidden [object]$Environment
    hidden [object]$Tenant
    hidden [object]$DeploymentPreview
    hidden [int]$DeploymentRetryCount
    hidden [int]$DeploymentAttempt
    
    DeploymentController($deploymentContext, $logPrefix, $environment, $tenant) {
        $this.BaseUrl = $deploymentContext.BaseUrl
        $this.DeploymentContext = $deploymentContext
        if (Test-String $logPrefix) {
            $this.LogPrefix = "[${logPrefix}] "
        }
        $this.Environment = $environment
        $this.Tenant = $tenant
        if ($tenant) {
            $this.DeploymentPreview = Invoke-OctopusApi ('/api/releases/{0}/deployments/preview/{1}/{2}' -f $this.DeploymentContext.Release.Id, $this.Environment.Id, $this.Tenant.Id)
        }
        else {
            $this.DeploymentPreview = Invoke-OctopusApi ('/api/releases/{0}/deployments/preview/{1}' -f $this.DeploymentContext.Release.Id, $this.Environment.Id)
        }
        $this.DeploymentRetryCount = $deploymentContext.DeploymentRetryCount
        $this.DeploymentAttempt = 0
    }

    hidden [string[]]$SkipActions = @()
    [void] SetStepsToSkip($stepsToSkip) {
        $comparisonArray = $stepsToSkip.Split("`n") | % Trim
        $this.SkipActions = $this.DeploymentPreview.StepsToExecute | ? {
            $_.CanBeSkipped -and ($_.ActionName -in $comparisonArray -or $_.ActionNumber -in $comparisonArray)
        } | % {
            $logMessage = "Skipping Step $($_.ActionNumber): $($_.ActionName)"
            if ($this.LogPrefix) { Write-Verbose "$($this.LogPrefix)$logMessage" }
            else { Write-Host $logMessage }
            $_.ActionId
        }
    }

    hidden [hashtable]$FormValues
    [void] SetFormValues($formValuesToSet) {
        $this.FormValues = @{}
        $this.DeploymentPreview.Form.Values | Get-Member -MemberType NoteProperty | % {
            $this.FormValues.Add($_.Name, $this.DeploymentPreview.Form.Values.$($_.Name))
        }

        $formValuesToSet.Split("`n") | % {
            $entry = $_.Split('=') | % Trim
            $this.DeploymentPreview.Form.Elements | ? { $_.Control.Name -ieq $entry[0] } | % {
                $logMessage = "Setting Form Value '$($_.Control.Label)' to: $($entry[1])"
                if ($this.LogPrefix) { Write-Verbose "$($this.LogPrefix)$logMessage" }
                else { Write-Host $logMessage }
                $this.FormValues[$_.Name] = $entry[1]
            }
        }
    }
	
    [ServerTask]$Task
    [void] Start() {
        $request = @{
            ReleaseId = $this.DeploymentContext.Release.Id
            EnvironmentId = $this.Environment.Id
            SkipActions = $this.SkipActions
            FormValues = $this.FormValues
            UseGuidedFailure = $this.DeploymentContext.UseGuidedFailure
        }
        if ($this.DeploymentContext.QueueTime -ne [datetime]::MinValue) { $request.Add('QueueTime', $this.DeploymentContext.QueueTime.ToUniversalTime().ToString('o')) }
        if ($this.DeploymentContext.QueueTimeExpiry -ne [datetime]::MinValue) { $request.Add('QueueTimeExpiry', $this.DeploymentContext.QueueTimeExpiry.ToUniversalTime().ToString('o')) }
        if ($this.Tenant) { $request.Add('TenantId', $this.Tenant.Id) }

        $deployment = Invoke-OctopusApi '/api/deployments' -Method Post -Body $request -GetErrorResponse
        if ($deployment.ErrorMessage) { Write-Fatal "$($deployment.ErrorMessage)`n$($deployment.Errors -join "`n")" }
        Write-Host "Queued $($deployment.Name)..."
        Write-Host "`t$($this.BaseUrl)$($deployment.Links.Web)"
        Write-Verbose "`t$($this.BaseUrl)$($deployment.Links.Self)"
        Write-Verbose "`t$($this.BaseUrl)/api/deploymentprocesses/$($deployment.DeploymentProcessId)"
        Write-Verbose "`t$($this.BaseUrl)$($deployment.Links.Variables)"
        Write-Verbose "`t$($this.BaseUrl)$($deployment.Links.Task)/details"

        $this.Task = [ServerTask]::new($this.DeploymentContext, $deployment, $this.LogPrefix)
    }

    [bool] PollCheck() {
        $this.Task.Poll()
        if ($this.Task.IsCompleted -and !$this.Task.FinishedSuccessfully -and $this.DeploymentAttempt -lt $this.DeploymentRetryCount) {
            $retryWaitPeriod = New-TimeSpan -Seconds (Get-OctopusSetting RetryWaitPeriod 0)
            $waitText = if ($retryWaitPeriod.TotalSeconds -gt 0) {
                $minutesText = if ($retryWaitPeriod.Minutes -gt 1) { " $($retryWaitPeriod.Minutes) minutes" } elseif ($retryWaitPeriod.Minutes -eq 1) { " $($retryWaitPeriod.Minutes) minute" }
                $secondsText = if ($retryWaitPeriod.Seconds -gt 1) { " $($retryWaitPeriod.Seconds) seconds" } elseif ($retryWaitPeriod.Seconds -eq 1) { " $($retryWaitPeriod.Seconds) second" }
                "Waiting${minutesText}${secondsText} before "
            }
            $this.DeploymentAttempt++
            Write-Error "$($this.LogPrefix)Deployment failed. ${waitText}Queuing retry #$($this.DeploymentAttempt) of $($this.DeploymentRetryCount)..."
            if ($retryWaitPeriod.TotalSeconds -gt 0) {
                Start-Sleep -Seconds $retryWaitPeriod.TotalSeconds
            }
            $this.Start()
            return $true
        }
        return !$this.Task.IsCompleted
    }
}

class ServerTask {
    hidden [DeploymentContext]$DeploymentContext
    hidden [object]$Deployment
    hidden [string]$LogPrefix

    hidden [bool] $IsCompleted = $false
    hidden [bool] $FinishedSuccessfully
    hidden [string] $ErrorMessage
    
    hidden [int]$PollCount = 0
    hidden [bool]$HasInterruptions = $false
    hidden [hashtable]$State = @{}
    hidden [System.Collections.Generic.HashSet[string]]$Logs
 
    ServerTask($deploymentContext, $deployment, $logPrefix) {
        $this.DeploymentContext = $deploymentContext
        $this.Deployment = $deployment
        $this.LogPrefix = $logPrefix
        $this.Logs = [System.Collections.Generic.HashSet[string]]::new()
    }
    
    [void] Poll() {	
        if ($this.IsCompleted) { return }

        $details = Invoke-OctopusApi ('/api/tasks/{0}/details?verbose=false&tail=30' -f $this.Deployment.TaskId)
        $this.IsCompleted = $details.Task.IsCompleted
        $this.FinishedSuccessfully = $details.Task.FinishedSuccessfully
        $this.ErrorMessage = $details.Task.ErrorMessage

        $this.PollCount++
        if ($this.PollCount % 10 -eq 0) {
            $this.Verbose("$($details.Task.State). $($details.Task.Duration), $($details.Progress.EstimatedTimeRemaining)")
        }
        
        if ($details.Task.HasPendingInterruptions) { $this.HasInterruptions = $true }
        $this.LogQueuePosition($details.Task)
        $activityLogs = $this.FlattenActivityLogs($details.ActivityLogs)    
        $this.WriteLogMessages($activityLogs)
    }

    hidden [bool] IfNewState($firstKey, $secondKey, $value) {
        $key = '{0}/{1}' -f $firstKey, $secondKey
        $containsKey = $this.State.ContainsKey($key)
        if ($containsKey) { return $false }
        $this.State[$key] = $value
        return $true
    }

    hidden [bool] HasChangedState($firstKey, $secondKey, $value) {
        $key = '{0}/{1}' -f $firstKey, $secondKey
        $hasChanged = if (!$this.State.ContainsKey($key)) { $true } else { $this.State[$key] -ne $value }
         if ($hasChanged) {
            $this.State[$key] = $value
         }
         return $hasChanged
    }

    hidden [object] GetState($firstKey, $secondKey) { return $this.State[('{0}/{1}' -f $firstKey, $secondKey)] }

    hidden [void] ResetState($firstKey, $secondKey) { $this.State.Remove(('{0}/{1}' -f $firstKey, $secondKey)) }

    hidden [void] Error($message) { Write-Error "$($this.LogPrefix)${message}" }
    hidden [void] Warn($message) { Write-Warning "$($this.LogPrefix)${message}" }
    hidden [void] Host($message) { Write-Host "$($this.LogPrefix)${message}" }   
    hidden [void] Verbose($message) { Write-Verbose "$($this.LogPrefix)${message}" }

    hidden [psobject[]] FlattenActivityLogs($ActivityLogs) {
        $flattenedActivityLogs = {@()}.Invoke()
        $this.FlattenActivityLogs($ActivityLogs, $null, $flattenedActivityLogs)
        return $flattenedActivityLogs
    }

    hidden [void] FlattenActivityLogs($ActivityLogs, $Parent, $flattenedActivityLogs) {
        foreach ($log in $ActivityLogs) {
            $log | Add-Member -MemberType NoteProperty -Name Parent -Value $Parent
            $insertBefore = $null -eq $log.Parent -and $log.Status -eq 'Running'	
            if ($insertBefore) { $flattenedActivityLogs.Add($log) }
            foreach ($childLog in $log.Children) {
                $this.FlattenActivityLogs($childLog, $log, $flattenedActivityLogs)
            }
            if (!$insertBefore) { $flattenedActivityLogs.Add($log) }
        }
    }

    hidden [void] LogQueuePosition($Task) {
        if ($Task.HasBeenPickedUpByProcessor) {
            $this.ResetState($Task.Id, 'QueuePosition')
            return
        }
		
        $queuePosition = (Invoke-OctopusApi ('/api/tasks/{0}/queued-behind' -f $this.Deployment.TaskId)).Items.Count
        if ($this.HasChangedState($Task.Id, 'QueuePosition', $queuePosition) -and $queuePosition -ne 0) {
            $this.Host("Queued behind $queuePosition tasks...")
        }
    }

    hidden [void] WriteLogMessages($ActivityLogs) {
        $interrupts = if ($this.HasInterruptions) {
            Invoke-OctopusApi ('/api/interruptions?regarding={0}' -f $this.Deployment.TaskId) | % Items
        }
        foreach ($activity in $ActivityLogs) {
            $correlatedInterrupts = $interrupts | ? CorrelationId -eq $activity.Id         
            $correlatedInterrupts | ? IsPending -eq $false | % { $this.LogInterruptMessages($activity, $_) }

            $this.LogStepTransition($activity)         
            $this.LogErrorsAndWarnings($activity)
            $correlatedInterrupts | ? IsPending -eq $true | % { 
                $this.LogInterruptMessages($activity, $_)
                $this.HandleInterrupt($_)
            }
        }
    }

    hidden [void] LogStepTransition($ActivityLog) {
        if ($ActivityLog.ShowAtSummaryLevel -and $ActivityLog.Status -ne 'Pending') {
            $existingState = $this.GetState($ActivityLog.Id, 'Status')
            if ($this.HasChangedState($ActivityLog.Id, 'Status', $ActivityLog.Status)) {
                $existingStateText = if ($existingState) {  "$existingState -> " }
                $this.Host("$($ActivityLog.Name) ($existingStateText$($ActivityLog.Status))")
            }
        }
    }

    hidden [void] LogErrorsAndWarnings($ActivityLog) {
        foreach ($logEntry in $ActivityLog.LogElements) {
            if ($logEntry.Category -eq 'Info') { continue }
            if ($this.Logs.Add(($ActivityLog.Id,$logEntry.OccurredAt,$logEntry.MessageText -join '/'))) {
                switch ($logEntry.Category) {
                    'Fatal' {
                        if ($ActivityLog.Parent) {
                            $this.Error("FATAL: During $($ActivityLog.Parent.Name)")
                            $this.Error("FATAL: $($logEntry.MessageText)")
                        }
                    }
                    'Error' { $this.Error("[$($ActivityLog.Parent.Name)] $($logEntry.MessageText)") }
                    'Warning' { $this.Warn("[$($ActivityLog.Parent.Name)] $($logEntry.MessageText)") }
                }
            }
        }
    }

    hidden [void] LogInterruptMessages($ActivityLog, $Interrupt) {
        $message = $Interrupt.Form.Elements | ? Name -eq Instructions | % Control | % Text
        if ($Interrupt.IsPending -and $this.HasChangedState($Interrupt.Id, $ActivityLog.Parent.Name, $message)) {
            $this.Warn("Deployment is paused at '$($ActivityLog.Parent.Name)' for manual intervention: $message")
        }
        if ($null -ne $Interrupt.ResponsibleUserId -and $this.HasChangedState($Interrupt.Id, 'ResponsibleUserId', $Interrupt.ResponsibleUserId)) {
            $user = Invoke-OctopusApi $Interrupt.Links.User
            $emailText = if (Test-String $user.EmailAddress) { " ($($user.EmailAddress))" }
            $this.Warn("$($user.DisplayName)$emailText has taken responsibility for the manual intervention")
        }
        $manualAction = $Interrupt.Form.Values.Result
        if ((Test-String $manualAction) -and $this.HasChangedState($Interrupt.Id, 'Action', $manualAction)) {
            $this.Warn("Manual intervention action '$manualAction' submitted with notes: $($Interrupt.Form.Values.Notes)")
        }
        $guidanceAction = $Interrupt.Form.Values.Guidance
        if ((Test-String $guidanceAction) -and $this.HasChangedState($Interrupt.Id, 'Action', $guidanceAction)) {
            $this.Warn("Failure guidance to '$guidanceAction' submitted with notes: $($Interrupt.Form.Values.Notes)")
        }
    }

    hidden [void] HandleInterrupt($Interrupt) {
        $isGuidedFailure = $null -ne ($Interrupt.Form.Elements | ? Name -eq Guidance)
        if (!$isGuidedFailure -or !$this.DeploymentContext.GuidedFailureActions -or !$Interrupt.IsPending) {
            return
        }
        $this.IfNewState($Interrupt.CorrelationId, 'ActionIndex', 0)
        if ($Interrupt.CanTakeResponsibility -and $null -eq $Interrupt.ResponsibleUserId) {
            Invoke-OctopusApi $Interrupt.Links.Responsible -Method Put
        }
        if ($Interrupt.HasResponsibility) {
            $guidanceIndex = $this.GetState($Interrupt.CorrelationId, 'ActionIndex')
            $guidance = $this.DeploymentContext.GuidedFailureActions[$guidanceIndex]
            $guidanceIndex++
            
            $retryWaitPeriod = New-TimeSpan -Seconds (Get-OctopusSetting RetryWaitPeriod 0)
            if ($guidance -eq 'Retry' -and $retryWaitPeriod.TotalSeconds -gt 0) {
                $minutesText = if ($retryWaitPeriod.Minutes -gt 1) { " $($retryWaitPeriod.Minutes) minutes" } elseif ($retryWaitPeriod.Minutes -eq 1) { " $($retryWaitPeriod.Minutes) minute" }
                $secondsText = if ($retryWaitPeriod.Seconds -gt 1) { " $($retryWaitPeriod.Seconds) seconds" } elseif ($retryWaitPeriod.Seconds -eq 1) { " $($retryWaitPeriod.Seconds) second" }
                $this.Warn("Waiting${minutesText}${secondsText} before submitting retry failure guidance...")
                Start-Sleep -Seconds $retryWaitPeriod.TotalSeconds
            }
            Invoke-OctopusApi $Interrupt.Links.Submit -Body @{
                Notes = $this.DeploymentContext.GuidedFailureMessage.Replace('#{GuidedFailureActionIndex}', $guidanceIndex).Replace('#{GuidedFailureAction}', $guidance)
                Guidance = $guidance
            } -Method Post

            $this.HasChangedState($Interrupt.CorrelationId, 'ActionIndex', $guidanceIndex)
        }
    }
}

function Show-Heading {
    param($Text)
    $padding = ' ' * ((80 - 2 - $Text.Length) / 2)
    Write-Host " `n"
    Write-Host (@("`t", ([string][char]0x2554), (([string][char]0x2550) * 80), ([string][char]0x2557)) -join '')
    Write-Host "`t$(([string][char]0x2551))$padding $Text $padding$([string][char]0x2551)"  
    Write-Host (@("`t", ([string][char]0x255A), (([string][char]0x2550) * 80), ([string][char]0x255D)) -join '')
    Write-Host " `n"
}

Write-Host 'Disabling remaining conventions...'
Set-OctopusVariable -Name 'Octopus.Action.SkipRemainingConventions' -Value 'True'

if (!(Test-IsNominatedDeployer)) {
	Write-Host "This machine is not the nominated deployer for this step, skipping..."
	return
}

$deployScript = $OctopusParameters['Octopus.Action.CustomScripts.Deploy.ps1'] 
if (Test-String $deployScript) {
	Show-Heading 'Pre-Deploy Script' 
	[scriptblock]::Create($deployScript).Invoke()
}

$deploymentContext = [DeploymentContext]::new($Chain_BaseUrl)

Show-Heading 'Loading Deployment Package'
Write-Verbose (Get-Content -Path (Join-Path $PSScriptRoot 'deploy.config') -Raw)
$deploymentContext.SetProject()
$deploymentContext.SetChannel()
Write-Host "`t$Chain_BaseUrl$($deploymentContext.Project.Links.Web)"
$deploymentContext.SetRelease()
Write-Host "`t$Chain_BaseUrl$($deploymentContext.Release.Links.Web)"
if ((Get-DeployConfigSetting "Octopus.Action.SnapshotVariables" "False") -ieq 'True') {
    $deploymentContext.UpdateVariableSnapshot()
}

Show-Heading 'Configuring Deployment'
$deploymentContext.GetDeploymentTemplate()
$email = if (Test-String $OctopusParameters['Octopus.Deployment.CreatedBy.EmailAddress']) { "($($OctopusParameters['Octopus.Deployment.CreatedBy.EmailAddress']))" }
$guidedFailureMessage = Get-OctopusSetting GuidedFailureMessage @"
Automatic Failure Guidance will #{GuidedFailureAction} (Failure ###{GuidedFailureActionIndex})
Initiated by $($OctopusParameters['Octopus.Deployment.Name']) of $($OctopusParameters['Octopus.Project.Name']) release $($OctopusParameters['Octopus.Release.Number'])
Created By: $($OctopusParameters['Octopus.Deployment.CreatedBy.DisplayName']) $email
${Chain_BaseUrl}$($OctopusParameters['Octopus.Web.DeploymentLink'])
"@
$deploymentContext.SetGuidedFailure((Get-DeployConfigSetting "Octopus.Action.GuidedFailure" "Default"), $guidedFailureMessage)
$deploymentContext.SetSchedule((Get-DeployConfigSetting "Octopus.Action.Schedule" "WaitForDeployment"))

$deploymentContext.SetEnvironment((Get-DeployConfigSetting "Octopus.Action.EnvironmentName" "#{Octopus.Environment.Name}" 'Octopus.Environment.Name'))
$deploymentContext.SetTenants((Get-DeployConfigSetting "Octopus.Action.TenantName" "#{Octopus.Deployment.Tenant.Name}" 'Octopus.Deployment.Tenant.Name'))

$deploymentControllers = $deploymentContext.GetDeploymentControllers()
$stepsToSkip = Get-DeployConfigSetting "Octopus.Action.StepsToSkip" ''
if (Test-String $stepsToSkip) {
    $deploymentControllers | % { $_.SetStepsToSkip($stepsToSkip) }
}
$formValues = Get-DeployConfigSetting "Octopus.Action.FormValues" ''
if (Test-String $formValues) {
    $deploymentControllers | % { $_.SetFormValues($formValues) }
}

Show-Heading 'Queue Deployment'
if ($deploymentContext.IsTenanted) {
    Write-Host 'Queueing tenant deployments...'
}
else {
    Write-Host 'Queueing untenanted deployment...'
}
$deploymentControllers | % Start

if (!$deploymentContext.WaitForDeployment) {
    Write-Host 'Deployments have been queued, proceeding to the next step...'
    return
}

Show-Heading 'Waiting For Deployment'
do {
    Start-Sleep -Seconds 1
    $tasksStillRunning = $false
    foreach ($deployment in $deploymentControllers) {
        if ($deployment.PollCheck()) {
            $tasksStillRunning = $true
        }
    }
} while ($tasksStillRunning)

if ($deploymentControllers | % Task | ? FinishedSuccessfully -eq $false) {
    Show-Heading 'Deployment Failed!'
    Write-Fatal (($deploymentControllers | % Task | % ErrorMessage) -join "`n")
}
else {
    Show-Heading 'Deployment Successful!'
}

$postDeployScript = $OctopusParameters['Octopus.Action.CustomScripts.PostDeploy.ps1'] 
if (Test-String $postDeployScript) {
	Show-Heading 'Post-Deploy Script' 
	[scriptblock]::Create($postDeployScript).Invoke()
}
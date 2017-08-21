# Octopus Deploy - NuGet Deployment Feed
A NuGet feed for Octopus Deploy which creates NuGet packages used to chain deployments of different Octopus projects together.

# Install
1. Use the **Octopus - Create Chain Deployment NuGet Feed micro service** community step template to provision & deploy the NuGet feed micro service. Once running all chain functionality is provided by the NuGet feed & the built-in **Deploy a package** step.

2. Add a **Deploy a package** step to the deployment process of an Octopus project with the package id containing the name of the Octopus project you wish to trigger the deployment of. The following parameters do not have a default value and must be provided:
- API Key
- Octopus Azure Account
- Virtual Network Resource Group Name
- Virtual Network Name
- Subnet Name

![Octopus - Deployment Process - Add Deploy Package Step](/Images/package-step.png)

3. Create a release. The Octopus projects being deployed follow the same behaviour as regular packages, so the release version being deployed is set when a release is made & channel package step version rules can be used to select stable or development channel releases appropriately.

![Octopus - Create Release](/Images/create-release.png)

4. Deploy!
![Octopus - Deployment](/Images/deploy.png)

![Octopus - Deployed](/Images/deployed.png)

# Manual Install
1. Deploy the NuGet feed from this repository, the following ARM template takes less than 10 minutes to run and if deployed onto a virtual network that has two way connectivity with Octopus Deploy is fully functional once the template finishes.

    <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpaulmarsy%2FOctopusNuGetDeploymentFeed%2Fmaster%2FProvisioning%2Ftemplate.json" target="_blank">
    <img src="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/deploytoazure.png"/>
    </a>
    <a href="http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2Fpaulmarsy%2FOctopusNuGetDeploymentFeed%2Fmaster%2FProvisioning%2Ftemplate.json" target="_blank">
    <img src="https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/visualizebutton.png"/>
    </a>

2. Create a new 'External Feed' in Octopus Deploy. The feed should be configured as follows:
  * **Feed Type** NuGet Feed
  * **URL** Address where the feed has been deployed, for example if the VM has the IP 1.2.3.4 enter http://1.2.3.4/nuget/
  * **Username** Base URL of the Octopus Server which the NuGet feed should connect to, for example https://my-octopus-server.com/
  * **Password** A valid Octopus Deploy API key to authenticate with
![Octopus - Library - External Feed](/Images/external-feed.png)

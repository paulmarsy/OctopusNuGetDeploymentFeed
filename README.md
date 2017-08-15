# Octopus Deploy - NuGet Deployment Feed
A NuGet feed of Octopus Deploy releases. Generates NuGet packages used to trigger deployments.

# Install
1. Deploy the NuGet feed from this repository

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

3. Add a **Deploy a package** step to the deployment process of an Octopus project
![Octopus - Deployment Process - Add Deploy Package Step](/Images/package-step.png)

4. Deploy!

![Octopus - Deployment](/Images/deploy.png)

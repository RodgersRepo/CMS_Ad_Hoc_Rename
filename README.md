# CMS Ad hoc rename

A dot net core minimal api application to change Ad hoc conference names on a Cisco Meeting Server (CMS). For deployments where CUCM (Cisco Unified Communications Manager) creates the conference on CMS.

if you are using CMS for your ad hoc CUCM conferences you have probably seen touch devices such as the Navigator displaying the name of the conference as a numeric string. Figure 1 below demonstrates this.

![Figure 1 - Touch Device Ad Hoc screen shot](/./AdHocScreenShot.png "CUCM Active calls script screenshot")

This web application will change that string to a string of your choosing. It requires a seperate microsoft IIS server to host the apllication. If you are already using IIS to host the [CMS branding files](https://www.cisco.com/c/dam/en/us/td/docs/conferencing/ciscoMeetingServer/Customisation/Version-3-5/Cisco-Meeting-Server-3-5-Customization-Guidelines.pdf) files this could be used.

The solution was tested in a lab enviroment connected as paer the diagram below. Whenever a ad hoc conference is requested the CMS server immeadeatly sends the call detail record to the IIS server. The server detcts this and returns a REST responces to alter the ad hoc space name. To get this to work please read the installation section and replace the defaults in the **Private/creds.csv** file with values of your chosing. A secure password can also be used, see installation for this also. 

## Installation
### To VSCODE
Clone the Repository:

Open a terminal or command prompt.
Navigate to the directory where you want to clone the repository.
Use the git clone command followed by the repository URL. For example:
```sh
git clone https://github.com/username/repository.git
```
Open the Project in Visual Studio Code:

Open Visual Studio Code.
Use the **File > Open Folder** menu option and select the folder where you cloned the repository.
Alternatively, you can open the terminal in VS Code and navigate to the cloned directory, then type:
```sh
code .
```
This application uses the directive **Serilog** to provide log messages to a file. You will need to add this to the project. From VSCODE terminal type:
```sh
dotnet add package Serilog
dotnet add package Serilog.Sinks.File
```

### To Microsoft Internet Information Services (IIS)
I deployed this to IIS as an application called **cmsrename** not a web site. Open the project in VSCODE. Open the command line interface **View > Terminal**, then type:
```sh
dotnet publish -c Release -o ./publish
```
This will compile the project to a folder named **project** on your development computer. 
Download and install .NET Core bundle onto the IIS target server that you intend to deploy this app to from Microsoft.

- Run the installer on the IIS server
- Restart the server or execute `net stop was /y` followed by `net start w3svc`
- On the IIS server, create a folder to contain the app's published folders and files called **cmsrename**. In a following step, the folder's path is provided to IIS as the physical path to the application. For more information on an app's deployment folder and file layout, see ASP.NET Core directory structure.
- In IIS Manager, open the server's node in the Connections panel. Right-click the **Sites/default web site** folder. Select **Add Application** from the contextual menu.
- Provide a Site alias and set the Physical path to the app's deployment folder that you created. Keep the Application Pool at the default (best practise is to have a seperate pool per application, see below for this). Create the application by selecting **OK**.
  
Copy the files from the **publish** folder on your development computer to the IIS server application folder. Test by browsing to `HTTP://<your server URL/cmsrename`.
To secure the site form the IIS manager, click your application name **cmsrenmae** then double click `SSL Settings`, set for your enviroment. For testing I selected **Require** and **ingor**. You will need to have certificates already set up on the IIS server for this to work.

PLEASE NOTE: You will have to amend the **index.html** file once it has been deployed to IIS. The routing will be wrong. Change lines 72 and 90 to include the application name `cmsrename`. Line 72 becomes:
```sh
<form action="/cmsrename/creds" method="post">
```
Line 90 becomes:
```sh
<form action="/cmsrename/logging" method="post">
```
Once installed browse to `https://<SERVER URL>/cmsrename/index.html`. Scroll down to **Credentials for this web app**. Enter the username and password the IIS server will use to send its REST API space name change request. You will get a new page with the encrypted password. Paste this into the **private/creds.csv** spread sheet. In the same sheet review and change the other headings for you enviroment.

## Usage

### Configuring CUCM for this web app

Configure a CUCM to CMS Ad hoc conference bridge using the Cisco document
[Configure Cisco Meeting Server and CUCM Ad hoc Conferences](https://www.cisco.com/c/en/us/support/docs/conferencing/meeting-server/213820-configure-cisco-meeting-server-and-cucm.html)

Log into CUCM administration, navigate to **Media Resources > Conference Bridge**. Click the hyperlink for the name you have configured for Ad hoc conferencing. In the Conference Bridge Prefix text box type the text string that will uniquely identify Ad hoc conferences. For example I have used **123456**. Click **Save** then **Apply Config**.

### Configuring CMS to use this web app

Log into your CMS server, then navigate to **Configuration > CDR settings**. In one of the Receiver URI text boxes type the following followed by **Submit**.
```sh
https://<SERVER URL>/cmsrename
```
cmsrename will only use https so please ensure your certificates are valid.

## Caveats
This application has only been tested in a lab enviroment with a single CUCM publisher and a single CMS.

## Credits and references

#### [Configure Cisco Meeting Server and CUCM Ad hoc Conferences](https://www.cisco.com/c/en/us/support/docs/conferencing/meeting-server/213820-configure-cisco-meeting-server-and-cucm.html)
Guide to configuring CMS ad hoc conferences for CUCM.
#### [Configure Cisco Meeting Server call detail records](https://www.cisco.com/c/dam/en/us/td/docs/conferencing/ciscoMeetingServer/Reference_Guides/Version-3-5/Cisco-Meeting-Server-CDR-Guide-3-5.pdf)
Cisco Meeting Server Call Detail Records Guide.
#### [Configure Cisco Meeting Server branding](https://www.cisco.com/c/dam/en/us/td/docs/conferencing/ciscoMeetingServer/Customisation/Version-3-5/Cisco-Meeting-Server-3-5-Customization-Guidelines.pdf)
A guide for configuring Cisco meeting sever branding. Includes the set up for seperate branding web servers
#### [CSS styling from Codestart](https://www.codersarts.com/post/html-forms-templates-using-css)
Great styling examples

----

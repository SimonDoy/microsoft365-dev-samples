'use strict';

const build = require('@microsoft/sp-build-web');

/* Start iThink365 Custom Build Tasks */
const del = require('del');
const fs = require('fs');
const argv = require('yargs').argv;
const packageJsonPath = './package.json';
const packageSolutionJsonPath = './config/package-solution.json';
const copyAssetsConfigFilePath = "./config/copy-assets.json";
const writeManifestPath = './config/write-manifests.json';
const deployAzureStoragePath = './config/deploy-azure-storage.json';
let packageVersion = "";
let packageSolutionVersion = "";


let readPackageJsonVersion = build.subTask('read-package-json-version', function (gulp, buildOptions, done) {
    return new Promise((resolve, reject) => {
        let json = JSON.parse(fs.readFileSync(packageJsonPath));
        packageVersion = json.version;
        this.log(`${packageJsonPath} version is ${packageVersion}`);
        resolve();
    });
});

let convertPackageJsonToSolutionVersion = build.subTask('convert-package-json-to-solution-version', function (gulp, buildOptions, done) {
    return new Promise((resolve, reject) => {
        let majorVersion = 0;
        let minorVersion = 0;
        let patchVersion = 0;
        let preReleaseVersion = 0;

        let positionOfPreReleaseVersion = -1;
        let hasPreReleaseVersionTag = false;

        this.log(`package version: ${packageVersion}`);

        let versionArray = packageVersion.split('.');
        majorVersion = versionArray[0];
        minorVersion = versionArray[1];
        patchVersion = versionArray[2];

        positionOfPreReleaseVersion = versionArray[2].indexOf('-');
        if (positionOfPreReleaseVersion >= 0) {
            hasPreReleaseVersionTag = true;
        }

        if (hasPreReleaseVersionTag) {
            this.log(`Found pre-release version tag: ${versionArray[2]}`);
            let preReleaseVersionArray = versionArray[2].split('-');

            patchVersion = preReleaseVersionArray[0];
            preReleaseVersion = preReleaseVersionArray[1];

            if (preReleaseVersion.match(/[a-z]/)) {
                console.warn(`pre-release version tag does not contain just numbers, please update. ${preReleaseVersion}`);
            }
        }

        let packageSolution = `${majorVersion}.${minorVersion}.${patchVersion}.${preReleaseVersion}`;
        packageSolutionVersion = packageSolution;
        this.log(`Package version: ${packageVersion} => ${packageSolutionVersion}`);
        resolve();
    });
});

let updatePackageSolutionManifestSubTask = build.subTask('update-package-solution-manifest-subtask', function (gulp, buildOptions, done) {
    return new Promise((resolve, reject) => {
        this.log(`updating package solution manifest.`);
        let json = JSON.parse(fs.readFileSync(packageSolutionJsonPath));
        json.solution.version = packageSolutionVersion;
        if (packageSolutionVersion.match(/[a-z]/)) {
            this.error(`found unexpected characters in the version number: ${packageSolutionVersion}.`);
        } else {
            fs.writeFileSync(packageSolutionJsonPath, JSON.stringify(json, null, 2));
        }
        resolve();
    });
});

let updateDeployAssetVersionPathSubTask = build.subTask('update-deploy-asset-version-path-subtask', function (gulp, buildOptions, done) {
    return readPackageJsonVersion.execute(buildOptions).then(result => {
        convertPackageJsonToSolutionVersion.execute(buildOptions).then(result => {
            updatePackageSolutionManifestSubTask.execute(buildOptions).then(result => {
                if (!argv.CDNENDPOINT) {
                    throw 'Missing CDNENDPOINT parameter';
                }

                let storageAccountCompatibleVersion = packageSolutionVersion.replace(/\./g, "-");
                let copyAssetsJson = JSON.parse(fs.readFileSync(copyAssetsConfigFilePath));
                copyAssetsJson.deployCdnPath = `temp/deploy/v${storageAccountCompatibleVersion}/`;

                this.log(`Saving ${copyAssetsConfigFilePath}`, copyAssetsJson);
                fs.writeFileSync(copyAssetsConfigFilePath, JSON.stringify(copyAssetsJson, null, 2));


                const azureCdnEndpoint = argv.CDNENDPOINT;
                const azureCdnContainerName = argv.STORAGEACCOUNTCONTAINER;

                // TODO: this needs to read the current json file check for a version and replace it.
                let cdnPathWithVersion = `${azureCdnEndpoint}/${azureCdnContainerName}/v${storageAccountCompatibleVersion}`;
                this.log(`Setting cdnBasePath = ${cdnPathWithVersion}`);

                let writeManifestsJson = JSON.parse(fs.readFileSync(writeManifestPath));
                writeManifestsJson.cdnBasePath = cdnPathWithVersion;

                this.log(`Saving ${writeManifestPath}`, writeManifestsJson);
                fs.writeFileSync(writeManifestPath, JSON.stringify(writeManifestsJson, null, 2));
            });
        });
    }).catch(error => {
        done(error);
    });
});

let updateProjectVersionSubTask = build.subTask('update-project-version-subtask', function (gulp, buildOptions, done) {
    this.log(`Updating project manifest files`);
    return updateDeployAssetVersionPathSubTask.execute(buildOptions).then((result) => {
        this.log(`Project manifest files updated.`);
        done();
    },
        (error) => {
            if (error) {
                done(error);
            };
        });
});

let cleanBundlingDirectorySubTask = build.subTask('clean-bundle-directory', function (gulp, buildOptions, done) {
    this.log('Cleaning up build directories.');
    return del(['dist', 'temp']);
});

let updateAzureStorageConfigSubTask = build.subTask('update-deploy-azure-config-from-env', function (gulp, buildOptions, done) {
    this.log('Updating deployment configuration from build.');
    return new Promise((resolve, reject) => {
        // update Deploy Azure JSON file.
        if (argv.STORAGEACCOUNTNAME && argv.STORAGEACCOUNTCONTAINER && argv.STORAGEACCOUNTTOKEN && argv.CDNENDPOINT) {
            let deployAzureJson = JSON.parse(fs.readFileSync(deployAzureStoragePath));

            deployAzureJson.account = argv.STORAGEACCOUNTNAME;
            deployAzureJson.container = argv.STORAGEACCOUNTCONTAINER;
            deployAzureJson.accessKey = argv.STORAGEACCOUNTTOKEN;

            this.log(`Saving ${deployAzureStoragePath}`, deployAzureJson);
            fs.writeFileSync(deployAzureStoragePath, JSON.stringify(deployAzureJson, null, 2));

            updateDeployAssetVersionPathSubTask.execute(buildOptions).then((result) => {
                resolve();
            });
        }
        else {
            reject('args not all defined, (STORAGEACCOUNTNAME, STORAGEACCOUNTCONTAINER, STORAGEACCOUNTTOKEN) are not defined.')
        }
    });
});

let updateVersionSubTask = build.subTask('update-package-version', function (gulp, buildOptions, done) {
    this.log('Updating package.json version number from build.');
    return new Promise((resolve, reject) => {
        if (argv.BUILDVERSION) {
            let packageJson = JSON.parse(fs.readFileSync(packageJsonPath));
            packageJson.version = argv.BUILDVERSION;
            this.log(`Updating ${packageJsonPath}`, packageJson);
            fs.writeFileSync(packageJsonPath, JSON.stringify(packageJson, null, 2));
            resolve();
        }
        else {
            reject('arg (BUILDVERSION) is not defined.')
        }
    });
});

let writeAzureStoreConfigTask = build.task('write-deploy-config', updateAzureStorageConfigSubTask);
build.task('update-deploy-asset-version-path', updateDeployAssetVersionPathSubTask);
let updateProjectVersionTask = build.task('update-project-version', updateProjectVersionSubTask);
let cleanBundleFoldersTask = build.task('clean-bundle-folders', cleanBundlingDirectorySubTask);
let updateVersionAndSolutionVersionTask = build.serial(updateVersionSubTask, readPackageJsonVersion, convertPackageJsonToSolutionVersion, updatePackageSolutionManifestSubTask);


let versionBuildTask = build.task('version', updateVersionAndSolutionVersionTask);
let releaseBuildTask = build.task('release', updateAzureStorageConfigSubTask);

// clean any build folders before building.
// remove comment to introduce into bundle process.
build.rig.addPreBuildTask(cleanBundleFoldersTask);


build.addSuppression(`Warning - [sass] The local CSS class 'ms-Grid' is not camelCase and will not be type-safe.`);

var getTasks = build.rig.getTasks;
build.rig.getTasks = function () {
  var result = getTasks.call(build.rig);

  result.set('serve', result.get('serve-deprecated'));

  return result;
};

build.initialize(require('gulp'));

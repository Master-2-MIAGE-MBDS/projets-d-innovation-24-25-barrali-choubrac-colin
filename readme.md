# README - DeepBridge DICOM Viewer

## Introduction

DeepBridge DICOM Viewer est une application de visualisation d'images DICOM en 2D et 3D. Elle a �t� d�velopp�e dans le cadre du projet DeepBridge, un projet de recherche en collaboration avec le CHU de Nice.

## Membres

- Cl�ment COLIN
- Thomas CHOUBRAC
- Florian BARRALI

## Installation et Configuration

## Pr�requis

Pour pouvoir ex�cuter et d�velopper ce projet, vous aurez besoin des �l�ments suivants :

- **.NET SDK** : version 8.0 ou sup�rieure
- **Visual Studio** : 2022 ou version ult�rieure avec les charges de travail suivantes :
  - D�veloppement .NET Desktop
  - D�veloppement Windows Universal Platform
  
- **Packages NuGet** (install�s automatiquement via le fichier projet) :
  - EvilDICOM (version 3.0.8998.340)
  - OpenTK (version 4.9.3)

### Configuration des donn�es

Pour utiliser l'application correctement, veuillez suivre ces �tapes pour l'installation des donn�es DICOM :

1. Cr�ez un dossier nomm� `dataset_chu_nice_2020_2021` � la racine de votre disque.
2. Extrayez l'ensemble des donn�es du scan dans ce dossier
3. La structure des dossiers doit �tre comme suit :
   ```
   C:\dataset_chu_nice_2020_2021\scan\[dossier patient]\[dossier �tude]\[s�rie d'images]\*.dcm
   ```

**Important** : L'utilisation du chemin � la racine du disque est n�cessaire pour �viter les probl�mes li�s � la limitation de longueur des chemins dans Windows et C#.

### Modification du chemin par d�faut

Pour configurer l'application avec vos donn�es DICOM, vous devez modifier le chemin du r�pertoire par d�faut dans le fichier `MainForm.cs` :

1. Ouvrez le fichier `MainForm.cs` dans Visual Studio
2. Localisez la ligne suivante (vers le d�but de la classe) :
   ```csharp
   private readonly string defaultDirectory = @"D:\ECOLE\dataset_chu_nice_2020_2021\scan\SF103E8_10.241.3.232_20210118173900817_CT\SF103E8_10.241.3.232_20210118173900817";
   ```

3. Remplacez-la par le chemin complet vers votre dossier de donn�es DICOM, par exemple :
   ```csharp
   private readonly string defaultDirectory = @"C:\dataset_chu_nice_2020_2021\scan\SF103E8_10.241.3.232_20210118173228207_CT_SR\SF103E8_10.241.3.232_20210118173228207";
   ```

## Format du chemin
Le format attendu est :
```
[Lecteur]:\dataset_chu_nice_2020_2021\scan\[dossier patient]\[dossier �tude]
```

**Important :** Assurez-vous que le chemin sp�cifi� dans `defaultDirectory` pointe vers le dossier parent qui contient les s�ries d'images DICOM, et non directement vers le dossier contenant les fichiers DICOM (*.dcm).

### Notes techniques

- **Compatibilit� graphique** : Les couleurs OpenGL fonctionnent correctement avec les cartes graphiques AMD, mais peuvent pr�senter des probl�mes avec les cartes NVIDIA.

## Fonctionnalit�s principales

- Visualisation des images DICOM en 2D
- Rendu 3D des structures anatomiques
- Localisation automatique du cou et des carotides
- Extraction de coupes personnalis�es avec contr�le des angles

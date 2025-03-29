# README - DeepBridge DICOM Viewer

## Introduction

DeepBridge DICOM Viewer est une application de visualisation d'images DICOM en 2D et 3D. Elle a été développée dans le cadre du projet DeepBridge, un projet de recherche en collaboration avec le CHU de Nice.

## Membres

- Clément COLIN
- Thomas CHOUBRAC
- Florian BARRALI

## Installation et Configuration

## Prérequis

Pour pouvoir exécuter et développer ce projet, vous aurez besoin des éléments suivants :

- **.NET SDK** : version 8.0 ou supérieure
- **Visual Studio** : 2022 ou version ultérieure avec les charges de travail suivantes :
  - Développement .NET Desktop
  - Développement Windows Universal Platform
  
- **Packages NuGet** (installés automatiquement via le fichier projet) :
  - EvilDICOM (version 3.0.8998.340)
  - OpenTK (version 4.9.3)

### Configuration des données

Pour utiliser l'application correctement, veuillez suivre ces étapes pour l'installation des données DICOM :

1. Créez un dossier nommé `dataset_chu_nice_2020_2021` à la racine de votre disque.
2. Extrayez l'ensemble des données du scan dans ce dossier
3. La structure des dossiers doit être comme suit :
   ```
   C:\dataset_chu_nice_2020_2021\scan\[dossier patient]\[dossier étude]\[série d'images]\*.dcm
   ```

**Important** : L'utilisation du chemin à la racine du disque est nécessaire pour éviter les problèmes liés à la limitation de longueur des chemins dans Windows et C#.

### Modification du chemin par défaut

Pour configurer l'application avec vos données DICOM, vous devez modifier le chemin du répertoire par défaut dans le fichier `MainForm.cs` :

1. Ouvrez le fichier `MainForm.cs` dans Visual Studio
2. Localisez la ligne suivante (vers le début de la classe) :
   ```csharp
   private readonly string defaultDirectory = @"D:\ECOLE\dataset_chu_nice_2020_2021\scan\SF103E8_10.241.3.232_20210118173900817_CT\SF103E8_10.241.3.232_20210118173900817";
   ```

3. Remplacez-la par le chemin complet vers votre dossier de données DICOM, par exemple :
   ```csharp
   private readonly string defaultDirectory = @"C:\dataset_chu_nice_2020_2021\scan\SF103E8_10.241.3.232_20210118173228207_CT_SR\SF103E8_10.241.3.232_20210118173228207";
   ```

## Format du chemin
Le format attendu est :
```
[Lecteur]:\dataset_chu_nice_2020_2021\scan\[dossier patient]\[dossier étude]
```

**Important :** Assurez-vous que le chemin spécifié dans `defaultDirectory` pointe vers le dossier parent qui contient les séries d'images DICOM, et non directement vers le dossier contenant les fichiers DICOM (*.dcm).

### Notes techniques

- **Compatibilité graphique** : Les couleurs OpenGL fonctionnent correctement avec les cartes graphiques AMD, mais peuvent présenter des problèmes avec les cartes NVIDIA.

## Fonctionnalités principales

- Visualisation des images DICOM en 2D
- Rendu 3D des structures anatomiques
- Localisation automatique du cou et des carotides
- Extraction de coupes personnalisées avec contrôle des angles

## Branche de refactorisation

Une branche nommée `refacto` est disponible dans le dépôt. Cette branche contient un travail en cours de refactorisation de l'application visant à :

- Améliorer l'architecture globale du code
- Renforcer la gestion de la mémoire avec une implémentation plus rigoureuse du pattern IDisposable
- Clarifier les responsabilités des différentes classes

Il est important de noter que ce travail de refactorisation n'est pas achevé. En particulier, la partie concernant le rendu 3D nécessite encore des améliorations significatives. 

Néanmoins, examiner cette branche peut être utile pour mieux comprendre la structure du projet.

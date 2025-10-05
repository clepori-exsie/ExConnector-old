# 🔨 Guide de compilation - ExConnector MSI

## 🎯 Objectif

Compiler un installateur MSI **professionnel** pour ExConnector avec :
- ✅ Service Windows (H24 en arrière-plan)
- ✅ Runtime .NET inclus (ZÉRO dépendance)
- ✅ Installation automatique
- ✅ Démarrage automatique

---

## 📋 Prérequis

### Outils nécessaires

1. **.NET SDK 9.0** (pour compiler)
2. **WiX Toolset v5** (pour créer le MSI)

### Installation de WiX v5

```powershell
dotnet tool install --global wix --version 5.0.2
```

Vérification :
```powershell
wix --version
# Doit afficher : 5.0.2+...
```

---

## 🚀 Compilation rapide (tout-en-un)

### Script automatique

```powershell
.\Build-MSI.ps1
```

**C'est tout !** Le script :
1. Compile l'application
2. Publie en mode self-contained
3. Génère le fichier WXS complet
4. Compile le MSI

Le fichier `ExConnector.msi` sera créé dans `WixInstaller\`

---

## 🔧 Compilation manuelle (étape par étape)

### Étape 1 : Nettoyer

```powershell
dotnet clean
Remove-Item -Path "bin","obj","publish" -Recurse -Force -ErrorAction SilentlyContinue
```

### Étape 2 : Publier

```powershell
dotnet publish -c Release -o publish
```

Résultat : Dossier `publish\` avec ~400 fichiers (~80-100 MB)

### Étape 3 : Générer le WXS

```powershell
cd WixInstaller
.\Generate-WXS.ps1
cd ..
```

Résultat : Fichier `Package-Full.wxs` avec tous les composants

### Étape 4 : Compiler le MSI

```powershell
cd WixInstaller
wix build Package-Full.wxs -o ExConnector.msi
cd ..
```

Résultat : `WixInstaller\ExConnector.msi` (~35 MB)

---

## ✅ Vérification

### Taille attendue

- **MSI** : ~35 MB
- **Contenu** : ~400 fichiers (runtime .NET + ExConnector)

### Test rapide

```powershell
# Vérifier que le MSI existe
Test-Path "WixInstaller\ExConnector.msi"

# Vérifier la taille
(Get-Item "WixInstaller\ExConnector.msi").Length / 1MB
# Doit afficher : ~35
```

---

## 📦 Distribution

### Fichier à distribuer

**`WixInstaller\ExConnector.msi`**

C'est le **SEUL fichier** à donner à vos consultants.

### Installation

Double-clic sur le MSI → Suivant → Suivant → Installer

**Pas de .NET à installer !**  
**Pas d'IIS à configurer !**  
**Tout est inclus !**

---

## 🐛 Dépannage

### Erreur : "wix: command not found"

WiX n'est pas installé. Solution :
```powershell
dotnet tool install --global wix --version 5.0.2
```

### Erreur : "Could not find part of the path"

Le dossier `publish\` n'existe pas. Solution :
```powershell
dotnet publish -c Release -o publish
```

### MSI trop petit (<1 MB)

Le WXS ne contient pas tous les fichiers. Solution :
```powershell
cd WixInstaller
.\Generate-WXS.ps1
wix build Package-Full.wxs -o ExConnector.msi
cd ..
```

### Erreur de compilation WiX

Vérifier la syntaxe XML du WXS :
```powershell
Get-Content WixInstaller\Package-Full.wxs | Select-String "error"
```

---

## 📝 Notes

### Pourquoi 35 MB ?

Le MSI contient :
- Runtime .NET 9 x86 : ~30 MB
- ExConnector + Objets Métiers : ~5 MB

C'est **normal** pour un package self-contained.

### Pourquoi self-contained ?

- ✅ Pas de dépendance .NET à installer
- ✅ Fonctionne sur n'importe quel Windows (même sans .NET)
- ✅ Installation rapide (1-2 min)
- ✅ Zéro configuration

### Architecture x86 ?

L'interop Sage (`Objets100cLib.dll`) est **32 bits**, donc ExConnector doit être en **x86**.

---

## 🔄 Mise à jour du MSI

### Incrémenter la version

Modifier `Package.wxs` ou `Package-Full.wxs` :

```xml
<Package Name="ExConnector"
         Version="1.1.0"  <!-- Changer ici -->
         ...>
```

Puis recompiler :
```powershell
.\Build-MSI.ps1
```

### GUID UpgradeCode

**NE JAMAIS CHANGER** le `UpgradeCode` !

```xml
UpgradeCode="8F5D4E3C-2A1B-4C9D-8E7F-3A2B1C9D8E7F"
```

C'est l'identifiant unique qui permet les mises à jour automatiques.

---

**Dernière mise à jour** : 2025-10-01


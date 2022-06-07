using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using RurouArtifacts.ModComponents;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace RurouArtifacts
{
    //This is an example plugin that can be put in BepInEx/plugins/ExamplePlugin/ExamplePlugin.dll to test out.
    //It's a small plugin that adds a relatively simple item to the game, and gives you that item whenever you press F2.

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]

    //This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    //We will be using 2 modules from R2API: ItemAPI to add our item and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI))]

    //This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class Artifacts : BaseUnityPlugin
    {
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Rurourin";
        public const string PluginName = "RurousArtifacts";
        public const string PluginVersion = "1.1.1";
        
        public static AssetBundle MainAssets;
        public  List<ArtifactBase> ArtifactsList  =  new  List<ArtifactBase>();
        
        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location) ?? string.Empty, "rurouartifacts");
            var assetRequest =   AssetBundle.LoadFromFileAsync(path);
            assetRequest.m_completeCallback += _ =>
            {
                MainAssets = assetRequest.assetBundle;
                var artifactTypes = Assembly.GetExecutingAssembly().GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ArtifactBase)));
                foreach (var artifactType in artifactTypes)
                {
                    ArtifactBase artifact = (ArtifactBase)Activator.CreateInstance(artifactType);
                    if (ValidateArtifact(artifact, ArtifactsList))
                    {
                        artifact.Init(Config, MainAssets);
                    }
                }
            };
            
        }

        public bool ValidateArtifact(ArtifactBase artifact, List<ArtifactBase> artifactList)
        {
            var value = Config.Bind<bool>("Artifact: " + artifact.ArtifactName, "Enable Artifact?", true, "Should this artifact appear for selection?").Value;
            if (value)
            {
                artifactList.Add(artifact);
            }
            return value;
        }
        //The Update() method is run on every frame of the game.
        private void Update()
        {
            foreach (var artifact in ArtifactsList)
            {
                artifact.Update();
            }
        }
    }
}

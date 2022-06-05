using BepInEx.Configuration;
using R2API;
using RoR2;
using UnityEngine;


namespace RurouArtifacts.ModComponents
{
    public abstract class ArtifactBase
    {
        public abstract string ArtifactName { get; }
        public abstract string ArtifactLangTokenName { get; }
        public abstract string ArtifactDescription { get; }
        public Sprite ArtifactEnabledIcon { get; set;}
        public Sprite ArtifactDisabledIcon { get; set; }
        public ArtifactDef ArtifactDef;
        public bool ArtifactEnabled => RunArtifactManager.instance.IsArtifactEnabled(ArtifactDef);
        public abstract void Init(ConfigFile config, AssetBundle bundle);
        protected void CreateLang()
        {
            LanguageAPI.Add("ARTIFACT_" + ArtifactLangTokenName + "_NAME", ArtifactName);
            LanguageAPI.Add("ARTIFACT_" + ArtifactLangTokenName + "_DESCRIPTION", ArtifactDescription);
        }
        protected virtual void CreateArtifact()
        {
            ArtifactDef = ScriptableObject.CreateInstance<ArtifactDef>();
            ArtifactDef.cachedName = "ARTIFACT_" + ArtifactLangTokenName;
            ArtifactDef.nameToken = "ARTIFACT_" + ArtifactLangTokenName + "_NAME";
            ArtifactDef.descriptionToken = "ARTIFACT_" + ArtifactLangTokenName + "_DESCRIPTION";
            ArtifactDef.smallIconSelectedSprite = ArtifactEnabledIcon;
            ArtifactDef.smallIconDeselectedSprite = ArtifactDisabledIcon;
            ContentAddition.AddArtifactDef(ArtifactDef);
        }
        public abstract void Hooks();

        public abstract void Update();
    }
}
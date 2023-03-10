/*
 * MToonシェーダー　→　UTS2シェーダーへ置換するUnityエディタ拡張スクリプト
 *
 * (C)2020 slip
 * This software is released under the MIT License.
 * http://opensource.org/licenses/mit-license.php
 * [Twitter]: https://twitter.com/kjmch2s/
 *
 * 利用規約：
 *  作者に無断で改変、再配布が可能で、利用形態（商用、18禁利用等）
 *  についても制限はありません。
 *  このスクリプトはもうあなたのものです。
 * 
 */

using System.Text;
using UnityEditor;
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;

namespace VRM
{
    public class Change_MToonToUTS2 : ScriptableWizard
    {
        private static GameObject m_Wizard;

        //対象になるモデル
        public GameObject targetModel;
        private readonly string OutputFolderName = "Assets/Models/Resources";

        private readonly string TextureFolderName = "Texture";
        private readonly string MaterialFolderName = "Material";

        private readonly string ShaderName_MToon = "VRM/MToon";
        private readonly string ShaderName_UTS2 = "UnityChanToonShader/Toon_DoubleShadeWithFeather_Clipping";

        public static void CreateWizard()
        {
            var wiz = ScriptableWizard.DisplayWizard<Change_MToonToUTS2>(
                "Change_MToonToUTS2", "change");
            var go = Selection.activeObject as GameObject;
            m_Wizard = new GameObject();

        }

        void OnWizardCreate()
        {
            SkinnedMeshRenderer[] skinnedMeshRenderers
             = targetModel.GetComponentsInChildren<SkinnedMeshRenderer>();

            //変更後のシェーダーを設定
            Shader changeShader = Shader.Find(ShaderName_UTS2);

            foreach(SkinnedMeshRenderer skinnedMeshRenderer in skinnedMeshRenderers){

                Material[] materials = skinnedMeshRenderer.sharedMaterials;

                for(int i = 0; i<materials.Length; i++){
                    Material material = materials[i];

                    if(material.shader.name == ShaderName_MToon){

                        Texture input_Tex = material.mainTexture;

                        //フォルダ作成
                        MakeFolderForShaders();

                        //MToonのMaterial書き出し(バックアップ用)
                        SaveMaterial(material,ShaderName_MToon);

                        //UTS2用にマスクテクスチャを作成
                        MakeMaskTexture(input_Tex);

                        //マテリアルの設定反映
                        SettingProperty_FromMtoonToUTS2(ref material,input_Tex,changeShader);

                        //UTS2のMaterial書き出し
                        SaveMaterial(material,ShaderName_UTS2);

                    }
                }
            }
        }

        void OnDestroy(){
            DestroyImmediate(m_Wizard);
        }

        void MakeFolderForShaders(){
            string shaderFolderName = null;

            shaderFolderName = ShaderName_MToon.Replace('/', '_');

            MakeFolder_OutputFiles(shaderFolderName);

            shaderFolderName = ShaderName_UTS2.Replace('/', '_');

            MakeFolder_OutputFiles(shaderFolderName);

        }

        void MakeFolder_OutputFiles(string shaderFolderName){
            string targetFolderPath = null;

            targetFolderPath = OutputFolderName + "/" + targetModel.name + "/" 
                + MaterialFolderName + "/" + shaderFolderName;

            if (!Directory.Exists(targetFolderPath)) {
                Directory.CreateDirectory(targetFolderPath);
            }

            targetFolderPath = OutputFolderName + "/" + targetModel.name + "/" 
                + TextureFolderName + "/" + shaderFolderName;

            if (!Directory.Exists(targetFolderPath)) {
                Directory.CreateDirectory(targetFolderPath);
            }
        }

        void MakeMaskTexture(Texture input_Tex){
            string shaderFolderName = null;

            Texture2D texture = 
            new Texture2D(input_Tex.width, input_Tex.height, TextureFormat.RGBA32, false);

            RenderTexture renderTexture = new RenderTexture(texture.width, texture.height, 32);

            // もとのテクスチャをRenderTextureにコピー
            Graphics.Blit(input_Tex, renderTexture);
            RenderTexture.active = renderTexture;
        
            // RenderTexture.activeの内容をtextureに書き込み
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            RenderTexture.active = null;
        
            // 不要になったので削除
            RenderTexture.DestroyImmediate(renderTexture);


            Color[] pixels = texture.GetPixels();

            for (int i = 0; i < pixels.Length; i++)
            {
                if(pixels[i].a > 0.0f){
                    pixels[i] = new Color(0,0,0,0);
                }
                else{
                    pixels[i] = new Color(1,1,1,1);
                }
            }

            texture.SetPixels(pixels);

            shaderFolderName = ShaderName_UTS2.Replace('/', '_');

            // pngとして保存
            System.IO.File.WriteAllBytes(OutputFolderName + "/" + targetModel.name + "/" + TextureFolderName + "/"
            +  shaderFolderName + "/" + input_Tex.name + "_clipping.png", texture.EncodeToPNG());
            
            AssetDatabase.Refresh();
        }

        void SettingProperty_FromMtoonToUTS2(ref Material material,Texture input_Tex,Shader changeShader){
            string shaderFolderName = null;

            Texture mainTex = material.GetTexture("_MainTex");
            Texture ShadeMap_1st = material.GetTexture("_ShadeTexture");
            Texture normalMap = material.GetTexture("_BumpMap");

            Texture matCap = material.GetTexture("_SphereAdd");
            Texture emissionMap = material.GetTexture("_EmissionMap");
            
            Color mainColor = material.GetColor("_Color");
            Color shadeColor = material.GetColor("_ShadeColor");

            Color emissionColor = material.GetColor("_EmissionColor");
            float outlineWidth = material.GetFloat("_OutlineWidth");
            Color outlineColor = material.GetColor("_OutlineColor");

            material.shader = changeShader;
            //material.SetFloat("_ClippingMode",1.0f);
            material.SetTexture("_BaseMap",mainTex);
            material.SetTexture("_1st_ShadeMap",ShadeMap_1st);
            material.SetTexture("_NormalMap",normalMap);
            material.SetColor("_BaseColor",mainColor);
            material.SetColor("_1st_ShadeColor",shadeColor);

            shaderFolderName = ShaderName_UTS2.Replace('/', '_');

            Texture maskTexture = Resources.Load(targetModel.name + "/" + TextureFolderName 
            +  "/" + shaderFolderName + "/" + input_Tex.name + "_clipping") as Texture;
            material.SetTexture("_ClippingMask",maskTexture);
            material.SetFloat("_Inverse_Clipping",1.0f);

            material.SetTexture("_MatCap_Sampler",matCap);
            material.SetTexture("_Emissive_Tex",emissionMap);
            material.SetColor("_Emissive_Color",emissionColor);
            material.SetFloat("_Outline_Width",outlineWidth);
            material.SetColor("_Outline_Color",outlineColor);            
        }

        void SaveMaterial(Material material,string ShaderName){
            Material outputMaterial = new Material(Shader.Find(ShaderName));
            outputMaterial.CopyPropertiesFromMaterial(material);

            string shaderFolderName = ShaderName.Replace('/', '_');

            AssetDatabase.CreateAsset(outputMaterial, OutputFolderName + "/" + targetModel.name + "/" 
            + MaterialFolderName + "/" + shaderFolderName + "/" + material.name + ".mat");
        }
    }

    public static class Change_MToonToUTS2_Menu
    {
        const string ADD_OPTIONOBJECT_KEY = VRMVersion.VRM_VERSION + "/MaterialSetting/Change_MToonToUTS2";

        [MenuItem(ADD_OPTIONOBJECT_KEY)]
        private static void Menu()
        {
           Change_MToonToUTS2.CreateWizard();
        }
    }
}
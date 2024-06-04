using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.IO;
using Object = UnityEngine.Object;

namespace HCZ
{
    public class ImageEditor : EditorWindow
    {
        private ObjectField textureField;
        private DropdownField alphaDropDown;
        private GradientField alphaGradient;
        private VisualElement imagePreview;
        private SliderInt alphaSlider;
        private Button exportButton;
        private string outputName;
        private Texture2D selectedTexture;
        private Texture2D outputTexture;
        private VisualElement customTexValues;
        private DropdownField textureOption;
        private IntegerField widthField;
        private IntegerField heightField;
        private Button createTexButton;
        private ComputeShader shader;
        private IntegerField alphaInput;
        private ColorField tint;

        [MenuItem("Tools/Image Editor")]
        public static void OpenEditorWindow()
        {
            ImageEditor wnd = GetWindow<ImageEditor>();
            wnd.titleContent = new GUIContent("Image Editor");
            wnd.maxSize = new Vector2(360, 520);
            wnd.minSize = wnd.maxSize;
        }

        private void CreateGUI()
        {
            shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/CardTemplate/Resources/AlphaShader.compute");
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Editor/CardTemplate/Elements/CardTemplateTree.uxml");
            VisualElement tree = visualTree.Instantiate();
            root.Add(tree);

            textureOption = root.Q<DropdownField>("texture-option");
            alphaDropDown = root.Q<DropdownField>("alpha-selection");
            textureField = root.Q<ObjectField>("texture-selection");
            alphaGradient = root.Q<GradientField>();
            imagePreview = root.Q<VisualElement>("image-preview");
            alphaSlider = root.Q<SliderInt>();
            exportButton = root.Q<Button>("export");
            customTexValues = root.Q<VisualElement>("texture-data");
            widthField = root.Q<IntegerField>("width-field");
            heightField = root.Q<IntegerField>("height-field");
            createTexButton = root.Q<Button>("create-button");
            tint = root.Q<ColorField>("tint");
            alphaInput = root.Q<IntegerField>("alpha-input");

            textureField.RegisterValueChangedCallback<Object>(TextureSelected);
            alphaDropDown.RegisterValueChangedCallback<string>(AlphaOptionSelected);
            textureOption.RegisterValueChangedCallback<string>(TextureOptionSelected);
            alphaSlider.RegisterValueChangedCallback<int>(AlphaSliderChanged);
            alphaInput.RegisterValueChangedCallback<int>(AlphaInputChanged);
            alphaGradient.RegisterValueChangedCallback<Gradient>(AlphaGradientChanged);
            tint.RegisterValueChangedCallback<Color>(TintChanged);
            exportButton.clicked += () => ExportImage(outputTexture);
            createTexButton.clicked += CreateTexture;

            imagePreview.style.backgroundImage = null;
            TextureOptionSelected(null);
            AlphaOptionSelected(null);
        }

        #region Button Methods
        private void CreateTexture()
        {
            int texWidth = widthField.value;
            int texHeight = heightField.value;
            selectedTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            for (int y = 0; y < texHeight; y++)
            {
                for (int x = 0; x < texWidth; x++)
                {
                    selectedTexture.SetPixel(x, y, Color.white);
                }
            }
            selectedTexture.Apply();
            outputName = "CustomTexture";
            ScaleToDisplay(texWidth, texHeight);

            ApplyAlphaGradient();
        }

        private void ScaleToDisplay(int texWidth, int texHeight)
        {
            bool greaterWidth = (texWidth > texHeight);
            float xRatio = 1;
            float yRatio = 1;
            if (greaterWidth)
            {
                yRatio = (float)texHeight / (float)texWidth;
            }
            else
            {
                xRatio = (float)texWidth / (float)texHeight;
            }
            imagePreview.style.width = 300 * xRatio;
            imagePreview.style.height = 300 * yRatio;
        }

        private void ExportImage(Texture2D texture2D)
        {
            var path = EditorUtility.SaveFilePanel(
                "Save Edited Texture", 
                Application.dataPath,
                outputName + ".png",
                "png");
            byte[] bytes = texture2D.EncodeToPNG();
            if(string.IsNullOrEmpty(path))
            {
                return;
            }
            File.WriteAllBytes(path, bytes);

            string pathString = path;
            int assetIndex = pathString.IndexOf("Assets", StringComparison.Ordinal);
            string filePath = pathString.Substring(assetIndex, path.Length - assetIndex);
            AssetDatabase.ImportAsset(filePath);
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = (Texture2D)AssetDatabase.LoadAssetAtPath(filePath, typeof(Texture2D));
            Debug.Log("File path is: " + filePath);
            Debug.Log("Path is: " + path);
        }
        #endregion
        #region Alpha Functions
        private void ApplyAlphaGradient()
        {
            if(selectedTexture == null)
            {
                exportButton.SetEnabled(false);
                return;
            }

            exportButton.SetEnabled(true);

            outputTexture = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);
            switch(alphaDropDown.index)
            {
                case 0:
                    outputTexture = AlphaWhole();
                    break;
                case 1:
                    outputTexture = GradientRight();
                    break;
                case 2:
                    outputTexture = GradientLeft();
                    break;
                case 3:
                    outputTexture = GradientBottom();
                    break;
                case 4:
                    outputTexture = GradientTop();
                    break;
            }
            imagePreview.style.backgroundImage = outputTexture;
        }

        private Texture2D GradientTop()
        {
            int kernelHandle = shader.FindKernel("GradientY");
            Texture2D selectedTex = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();
            RenderTexture gradientTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            gradientTex.enableRandomWrite = true;
            gradientTex.Create();
            Texture2D gradientTexture = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);

            for (int i = 0; i < selectedTexture.height; i++)
            {
                var target = alphaGradient.value.Evaluate((float)i / (float)selectedTexture.height);
                for (int j = 0; j < selectedTexture.width; j++)
                {
                    gradientTexture.SetPixel(j, i, target);
                }
            }
            gradientTexture.Apply();
            RenderTexture.active = gradientTex;
            Graphics.Blit(gradientTexture, gradientTex);
            shader.SetTexture(kernelHandle, "gradient", gradientTex);

            RenderTexture.active = inputTex;
            Graphics.Blit(selectedTexture, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);
            shader.SetVector("tint", tint.value);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }

        private Texture2D GradientBottom()
        {
            int kernelHandle = shader.FindKernel("GradientY");
            Texture2D selectedTex = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();
            RenderTexture gradientTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            gradientTex.enableRandomWrite = true;
            gradientTex.Create();
            Texture2D gradientTexture = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);

            for (int i = 0; i < selectedTexture.height; i++)
            {
                var target = alphaGradient.value.Evaluate(1.0f - ((float)i / (float)selectedTexture.height));
                for (int j = 0; j < selectedTexture.width; j++)
                {
                    gradientTexture.SetPixel(j, i, target);
                }
            }
            gradientTexture.Apply();
            RenderTexture.active = gradientTex;
            Graphics.Blit(gradientTexture, gradientTex);
            shader.SetTexture(kernelHandle, "gradient", gradientTex);

            RenderTexture.active = inputTex;
            Graphics.Blit(selectedTexture, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);
            shader.SetVector("tint", tint.value);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }

        private Texture2D GradientLeft()
        {
            int kernelHandle = shader.FindKernel("GradientX");
            Texture2D selectedTex = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();
            RenderTexture gradientTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            gradientTex.enableRandomWrite = true;
            gradientTex.Create();
            Texture2D gradientTexture = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);

            for (int i = 0; i < selectedTexture.width; i++)
            {
                var target = alphaGradient.value.Evaluate((float)i / (float)selectedTexture.height);
                for (int j = 0; j < selectedTexture.height; j++)
                {
                    gradientTexture.SetPixel(i, j, target);
                }
            }
            gradientTexture.Apply();
            RenderTexture.active = gradientTex;
            Graphics.Blit(gradientTexture, gradientTex);
            shader.SetTexture(kernelHandle, "gradient", gradientTex);

            RenderTexture.active = inputTex;
            Graphics.Blit(selectedTexture, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);
            shader.SetVector("tint", tint.value);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }

        private Texture2D GradientRight()
        {
            int kernelHandle = shader.FindKernel("GradientX");
            Texture2D selectedTex = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();
            RenderTexture gradientTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            gradientTex.enableRandomWrite = true;
            gradientTex.Create();
            Texture2D gradientTexture = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);

            for (int i = 0; i < selectedTexture.width; i++)
            {
                var target = alphaGradient.value.Evaluate(1.0f - ((float)i / (float)selectedTexture.width));
                for (int j = 0; j < selectedTexture.height; j++)
                {
                    gradientTexture.SetPixel(i, j, target);
                }
            }
            gradientTexture.Apply();
            RenderTexture.active = gradientTex;
            Graphics.Blit(gradientTexture, gradientTex);
            shader.SetTexture(kernelHandle, "gradient", gradientTex);

            RenderTexture.active = inputTex;
            Graphics.Blit(selectedTexture, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);
            shader.SetVector("tint", tint.value);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }

        private Texture2D AlphaWhole()
        {
            float alpha = (float)alphaSlider.value / 255f;
            return TexOpacity(alpha);
        }

        private int SetRunCount(int dimensionSize)
        {
            int count = dimensionSize / 8;
            if(dimensionSize%8>0)
            {
                count++;
            }
            return count;
        }
        private Texture2D TexOpacity(float alpha)
        {
            int kernelHandle = shader.FindKernel("AlphaWhole");
            Texture2D selectedTex = new Texture2D(selectedTexture.width, selectedTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(selectedTexture.width, selectedTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();

            RenderTexture.active = inputTex;
            Graphics.Blit(selectedTexture, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);
            shader.SetFloat("opacity", alpha);
            shader.SetVector("tint", tint.value);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }
        #endregion
        #region Event Callbacks
        private void TintChanged(ChangeEvent<Color> evt)
        {
            ApplyAlphaGradient();
        }

        private void AlphaGradientChanged(ChangeEvent<Gradient> evt)
        {
            ApplyAlphaGradient();
        }

        private void AlphaInputChanged(ChangeEvent<int> evt)
        {
            alphaSlider.SetValueWithoutNotify(evt.newValue);
            ApplyAlphaGradient();
        }

        private void AlphaSliderChanged(ChangeEvent<int> evt)
        {
            alphaInput.SetValueWithoutNotify(evt.newValue);
            ApplyAlphaGradient();
        }

        private void TextureOptionSelected(ChangeEvent<string> evt)
        {
            
            if(textureOption.value != textureOption.choices[0])
            {
                textureField.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                customTexValues.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
            else
            {
                textureField.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                customTexValues.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            }
            selectedTexture = null;
            textureField.value = null;
            imagePreview.style.backgroundImage = null;
            ApplyAlphaGradient();
        }

        private void AlphaOptionSelected(ChangeEvent<string> evt)
        {
            if (alphaDropDown.value != alphaDropDown.choices[0])
            {
                alphaSlider.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                alphaGradient.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            }
            else
            {
                alphaSlider.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                alphaGradient.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
            ApplyAlphaGradient();
        }

        private void TextureSelected(ChangeEvent<Object> evt)
        {
            if(evt.newValue == null)
            {
                selectedTexture = null;
                imagePreview.style.backgroundImage = null;
                return;
            }
            outputName = evt.newValue.name + "_Edited";
            selectedTexture = evt.newValue as Texture2D;
            ScaleToDisplay(selectedTexture.width, selectedTexture.height);
            ApplyAlphaGradient();
        }
        #endregion
    }
}


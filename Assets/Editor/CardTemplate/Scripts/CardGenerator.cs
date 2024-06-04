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
    public class CardGenerator : EditorWindow
    {
        class CardData
        {
            public CardData()
            {

            }
            public string name;
            public List<string> segmentCode = new List<string>();
        }

        private ComputeShader shader;
        private ObjectField ctgField;
        private ObjectField listField;
        private Toggle togglePrefix;
        private TextField prefixText;
        private TextField directoryNameField;
        private DropdownField formatField;
        private Button generateButton;

        Texture2D cardTexture;

        [MenuItem("Tools/Card Generator")]
        public static void OpenEditorWindow()
        {
            CardGenerator wnd = GetWindow<CardGenerator>();
            wnd.titleContent = new GUIContent("Card Generator");
            wnd.maxSize = new Vector2(350, 175);
            wnd.minSize = wnd.maxSize;
        }

        private void CreateGUI()
        {
            shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/CardTemplate/Resources/AlphaShader.compute");
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Editor/CardTemplate/Elements/CardGeneratorTree.uxml");
            VisualElement tree = visualTree.Instantiate();
            root.Add(tree);

            ctgField = root.Q<ObjectField>("ctg-file");
            listField = root.Q<ObjectField>("list-file");
            togglePrefix = root.Q<Toggle>("toggle-prefix");
            prefixText = root.Q<TextField>("prefix-name");
            directoryNameField = root.Q<TextField>("directory-name");
            formatField = root.Q<DropdownField>("exp-format");
            generateButton = root.Q<Button>("generate");

            ctgField.RegisterValueChangedCallback<Object>(CTGAssigned);
            listField.RegisterValueChangedCallback<Object>(FieldAssigned);
            prefixText.RegisterValueChangedCallback<string>(ChangedPrefix);
            directoryNameField.RegisterValueChangedCallback<string>(ChangedDirectory);
            generateButton.clicked += GenerateCards;

            generateButton.SetEnabled(false);
        }

        private void GenerateCards()
        {
            var path = EditorUtility.SaveFilePanel(
                "Save Generated Cards",
                Application.dataPath,
                directoryNameField.value,
                "");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            Directory.CreateDirectory(path);

            List<List<CardTemplateGenerator.SegInterElem>> interpretations = new List<List<CardTemplateGenerator.SegInterElem>>();
            List<CardTemplateGenerator.CardTempSeg> templateSegments = new List<CardTemplateGenerator.CardTempSeg>();
            List<int> backgroundIndexes = new List<int>();
            List<CardData> cardDatas = new List<CardData>();
            CardData loadingData = new CardData();
            CardTemplateGenerator.SegInterElem loadingInterpretation = new CardTemplateGenerator.SegInterElem();
            CardTemplateGenerator.CardTempSeg loadingSegment = new CardTemplateGenerator.CardTempSeg();
            int segmentResolution = 0;
            string ctgPath = AssetDatabase.GetAssetPath(ctgField.value);
            string template = File.ReadAllText(ctgPath);
            string[] lineBrokenTemplate = template.Split("\n");
            string[] dimensionString = lineBrokenTemplate[0].Split(";");
            Vector2Int dimensions = new Vector2Int(int.Parse(dimensionString[0]), int.Parse(dimensionString[1]));
            bool ignoreFirst = true;
            Texture2D workingTexture = null;
            byte[] textureData;

            bool ignoreFirstLine = true;
            foreach(var lineString in lineBrokenTemplate)
            {
                if (!ignoreFirstLine)
                {
                    if (lineString.Contains(":") && !lineString.Contains(">"))
                    {
                        segmentResolution = 0;
                        List<CardTemplateGenerator.SegInterElem> newList = new List<CardTemplateGenerator.SegInterElem>();
                        interpretations.Add(newList);
                        if (!ignoreFirst)
                        {
                            templateSegments.Add(loadingSegment);
                            loadingSegment = new CardTemplateGenerator.CardTempSeg();
                        }
                        else
                        {
                            ignoreFirst = false;
                        }
                    }
                    switch (segmentResolution)
                    {
                        case 0:
                            loadingSegment.name = lineString.Remove(lineString.Length - 1);
                            break;
                        case 1:
                            dimensionString = lineString.Split(";");
                            loadingSegment.position = new Vector2Int(int.Parse(dimensionString[0]), int.Parse(dimensionString[1]));
                            break;
                        case 2:
                            dimensionString = lineString.Split(";");
                            loadingSegment.size = new Vector2Int(int.Parse(dimensionString[0]), int.Parse(dimensionString[1]));
                            break;
                        case 3:
                            loadingSegment.layer = int.Parse(lineString);
                            break;
                        case 4:
                            backgroundIndexes.Add(int.Parse(lineString));
                            break;
                        case 5:
                            loadingSegment.background_alpha = int.Parse(lineString);
                            break;
                        case 6:
                            loadingSegment.interpreter_alpha = int.Parse(lineString);
                            break;
                        default:
                            if (lineString.Contains(">"))
                            {
                                loadingInterpretation.text_key = lineString.Substring(0, lineString.LastIndexOf(">"));
                                loadingInterpretation.answer_tex = new Texture2D(2, 2);
                                string pathForTex = RemoveInvalidFilenameCharacters(Path.GetDirectoryName(ctgPath) + "\\" + Path.GetFileNameWithoutExtension(ctgPath) + "\\" + "Tex" + lineString.Substring(lineString.LastIndexOf(">"), lineString.Length - lineString.LastIndexOf(">")) + ".png");
                                if (System.IO.File.Exists(pathForTex))
                                {
                                    textureData = System.IO.File.ReadAllBytes(pathForTex);
                                    loadingInterpretation.answer_tex.LoadImage(textureData);
                                    loadingInterpretation.answer_tex.Apply();
                                }
                                interpretations[templateSegments.Count].Add(loadingInterpretation);
                                loadingInterpretation = new CardTemplateGenerator.SegInterElem();
                            }
                            break;
                    }
                    segmentResolution++;
                }
                else
                {
                    ignoreFirstLine = false;
                }
            }
            templateSegments.Add(loadingSegment);
            segmentResolution = 0;
            string listFile = File.ReadAllText(AssetDatabase.GetAssetPath(listField.value));
            string[] brokenListFile = listFile.Split("\n");
            foreach(var lineString in brokenListFile)
            {
                if(segmentResolution == 0)
                {
                    loadingData.name = lineString;
                }
                else
                {
                    if(segmentResolution<templateSegments.Count)
                    {
                        loadingData.segmentCode.Add(lineString);
                    }
                    else
                    {
                        loadingData.segmentCode.Add(lineString);
                        cardDatas.Add(loadingData);
                        loadingData = new CardData();
                        segmentResolution = -1;
                    }
                }
                segmentResolution++;
            }

            foreach (var cardToGen in cardDatas)
            {
                cardTexture = new Texture2D(dimensions.x, dimensions.y, TextureFormat.RGBA32, false);
                for (int i = 0; i < cardTexture.width; i++)
                {
                    for (int j = 0; j < cardTexture.height; j++)
                    {
                        cardTexture.SetPixel(i, j, Color.clear);
                    }
                }
                cardTexture.Apply();
                foreach (var cardSegment in templateSegments)
                {
                    cardSegment.drawn_preview = false;
                }
                Vector2Int target = new Vector2Int(CardTemplateGenerator.MAX_LAYERS + 1, 0);
                for (int i = 0; i < templateSegments.Count; i++)
                {
                    for (int j = 0; j < templateSegments.Count; j++)
                    {
                        if (!templateSegments[j].drawn_preview && templateSegments[j].layer < target.x)
                        {
                            target.x = templateSegments[j].layer;
                            target.y = j;
                        }
                    }
                    templateSegments[target.y].drawn_preview = true;
                    var pathForTex = RemoveInvalidFilenameCharacters(Path.GetDirectoryName(ctgPath) + "\\" + Path.GetFileNameWithoutExtension(ctgPath) + "\\" + "Tex" + backgroundIndexes[target.y].ToString() + ".png");
                    if (System.IO.File.Exists(pathForTex))
                    {
                        textureData = System.IO.File.ReadAllBytes(pathForTex);
                        workingTexture = new Texture2D(2, 2);
                        workingTexture.LoadImage(textureData);
                        workingTexture.Apply();
                    }
                    else
                    {
                        Debug.Log("Failed to load:");
                        Debug.Log(pathForTex);
                    }
                    workingTexture = TexOpacity(workingTexture, templateSegments[target.y].position, templateSegments[target.y].size, templateSegments[target.y].background_alpha / 255f);
                    workingTexture.Apply();
                    cardTexture = WeCooking(cardTexture, workingTexture);
                    cardTexture.Apply();
                    byte[] testWrite = cardTexture.EncodeToPNG();
                    Vector2Int offset = new Vector2Int(0, 0);
                    foreach (var codeHit in InterpretTheString(cardToGen.segmentCode[target.y], interpretations[target.y]))
                    {
                        Texture2D decalTexture = new Texture2D(cardTexture.width, cardTexture.height, TextureFormat.RGBA32, false);
                        decalTexture = TexOpacity(interpretations[target.y][codeHit].answer_tex, templateSegments[target.y].position, templateSegments[target.y].size, offset, templateSegments[target.y].interpreter_alpha / 255f);
                        decalTexture.Apply();
                        cardTexture = WeCooking(cardTexture, decalTexture);
                        cardTexture.Apply();
                        offset.x += interpretations[target.y][codeHit].answer_tex.width;
                    }
                    cardTexture.Apply();
                    target.x = CardTemplateGenerator.MAX_LAYERS + 1;
                }
                byte[] writeBytes = cardTexture.EncodeToPNG();
                string generationName;

                if (togglePrefix.value)
                {
                    generationName = path + "/" + prefixText.value + cardToGen.name + ".png";
                }
                else
                {
                    generationName = path + "/" + cardToGen.name + ".png";
                }
                generationName = RemoveInvalidFilenameCharacters(generationName);
                File.WriteAllBytes(generationName, writeBytes);

                string pathString = path;
                int assetIndex = pathString.IndexOf("Assets", StringComparison.Ordinal);
                string filePath = pathString.Substring(assetIndex, path.Length - assetIndex);
                AssetDatabase.ImportAsset(filePath);
            }
            AssetDatabase.Refresh();
        }

        public static string RemoveInvalidFilenameCharacters(string filename)
        {
            int i;
            if (!string.IsNullOrEmpty(filename))
            {
                List<char> invalidchars = new List<char>();
		        invalidchars.AddRange(System.IO.Path.GetInvalidPathChars());
                //invalidchars.AddRange(System.IO.Path.GetInvalidFileNameChars());
                //invalidchars.AddRange(new char[] { System.IO.Path.PathSeparator, System.IO.Path.AltDirectorySeparatorChar });
                for (i = 0; i < invalidchars.Count; ++i)
                {
                    filename = filename.Replace(invalidchars[i].ToString(), string.Empty);
                }
            }
            return filename;
        }

        private void ChangedDirectory(ChangeEvent<string> evt)
        {
            directoryNameField.value = evt.newValue;
        }

        private void ChangedPrefix(ChangeEvent<string> evt)
        {
            prefixText.value = evt.newValue;
        }

        private void FieldAssigned(ChangeEvent<Object> evt)
        {
            if (ctgField.value != null && listField.value != null)
            {
                generateButton.SetEnabled(true);
            }
            else
            {
                generateButton.SetEnabled(false);
            }
        }

        private void CTGAssigned(ChangeEvent<Object> evt)
        {
            if(ctgField.value != null && listField.value != null)
            {
                generateButton.SetEnabled(true);
            }
            else
            {
                generateButton.SetEnabled(false);
            }
        }

        #region should have thought about this
        public List<int> InterpretTheString(string targetText, List<CardTemplateGenerator.SegInterElem> keys)
        {
            List<int> answer = new List<int>();
            List<Vector2Int> found = new List<Vector2Int>();
            bool key_hit = true;
            for (int h = 0; h < keys.Count; h++)
            {
                for (int j = 0; j < targetText.Length; j++)
                {
                    for (int i = 0; key_hit && i < keys[h].text_key.Length; i++)
                    {
                        if (j + keys[h].text_key.Length > targetText.Length)
                        {
                            key_hit = false;
                        }
                        else
                        {
                            if (targetText[j + i] != keys[h].text_key[i])
                            {
                                key_hit = false;
                            }
                        }
                    }
                    if (key_hit)
                    {
                        Vector2Int record_result = new Vector2Int(h, j);
                        found.Add(record_result);
                    }
                    else
                    {
                        key_hit = true;
                    }
                }
            }
            Vector2Int record_comparison = new Vector2Int(0, targetText.Length + 1);
            bool[] handled = new bool[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                for (int j = 0; j < found.Count; j++)
                {
                    if (!handled[j] && record_comparison.y > found[j].y)
                    {
                        record_comparison.x = j;
                        record_comparison.y = found[j].y;
                    }
                }
                handled[record_comparison.x] = true;
                answer.Add(found[record_comparison.x].x);
                record_comparison.y = targetText.Length + 1;
            }
            return answer;
        }
        private Texture2D WeCooking(Texture2D inputTexture, Texture2D cookingTexture)
        {
            int kernelHandle = shader.FindKernel("CookOnTop");
            Texture2D selectedTex = new Texture2D(cardTexture.width, cardTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(cardTexture.width, cardTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(cardTexture.width, cardTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();
            RenderTexture cookingTex = new RenderTexture(cardTexture.width, cardTexture.height, 32);
            cookingTex.enableRandomWrite = true;
            cookingTex.Create();

            RenderTexture.active = inputTex;
            Graphics.Blit(inputTexture, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = cookingTex;
            Graphics.Blit(cookingTexture, cookingTex);
            shader.SetTexture(kernelHandle, "cooking", cookingTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }

        private Texture2D TexOpacity(Texture2D inputTexture, Vector2Int location, Vector2Int size, float alpha)
        {
            int kernelHandle = shader.FindKernel("AlphaWhole");
            Texture2D selectedTex = new Texture2D(cardTexture.width, cardTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(cardTexture.width, cardTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(cardTexture.width, cardTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();

            for (int i = 0; i < cardTexture.width; i++)
            {
                for (int j = 0; j < cardTexture.height; j++)
                {
                    selectedTex.SetPixel(i, j, Color.clear);
                }
            }
            for (int i = 0; i < size.x; i++)
            {
                for (int j = 0; j < size.y; j++)
                {
                    selectedTex.SetPixel(i + location.x, j + location.y, inputTexture.GetPixel(i, j));
                }
            }
            selectedTex.Apply();

            RenderTexture.active = inputTex;
            Graphics.Blit(selectedTex, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);
            shader.SetFloat("opacity", alpha);
            shader.SetVector("tint", Color.white);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }

        private Texture2D TexOpacity(Texture2D inputTexture, Vector2Int location, Vector2Int size, Vector2Int offset, float alpha)
        {
            int kernelHandle = shader.FindKernel("AlphaWhole");
            Texture2D selectedTex = new Texture2D(cardTexture.width, cardTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(cardTexture.width, cardTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(cardTexture.width, cardTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();

            for (int i = 0; i < cardTexture.width; i++)
            {
                for (int j = 0; j < cardTexture.height; j++)
                {
                    selectedTex.SetPixel(i, j, Color.clear);
                }
            }
            for (int i = offset.x; i < size.x && i - offset.x < inputTexture.width; i++)
            {
                for (int j = offset.y; j < size.y && j - offset.y < inputTexture.height; j++)
                {
                    selectedTex.SetPixel(i + location.x, j + location.y, inputTexture.GetPixel(i - offset.x, j - offset.y));
                }
            }
            selectedTex.Apply();

            RenderTexture.active = inputTex;
            Graphics.Blit(selectedTex, inputTex);
            shader.SetTexture(kernelHandle, "Input", inputTex);
            RenderTexture.active = resultTex;
            shader.SetTexture(kernelHandle, "Result", resultTex);
            shader.SetFloat("opacity", alpha);
            shader.SetVector("tint", Color.white);

            shader.Dispatch(kernelHandle, SetRunCount(resultTex.width), SetRunCount(resultTex.height), 1);
            selectedTex.ReadPixels(new Rect(0, 0, resultTex.width, resultTex.height), 0, 0);
            selectedTex.Apply();
            RenderTexture.active = null;
            return selectedTex;
        }
        private int SetRunCount(int dimensionSize)
        {
            int count = dimensionSize / 8;
            if (dimensionSize % 8 > 0)
            {
                count++;
            }
            return count;
        }
        #endregion
    }
}

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
    public class CardTemplateGenerator : EditorWindow
    {
        #region Data Structures
        public class SegInterElem
        {
            public string text_key;
            public Texture2D answer_tex;
        }
        public class CardTempSeg
        {
            public CardTempSeg()
            { 

            }
            public string name;
            public int layer;
            public Vector2Int position;
            public Vector2Int size;
            public Texture2D background_img;
            public Texture2D render_texture;
            public int background_alpha;
            public int horizontal_index;
            public int vertical_index;
            public int interpreter_alpha;
            public string example;
            public List<SegInterElem> interpreter = new List<SegInterElem>();
            public bool drawn_preview;
        }
        #endregion

        #region variables
        const int BASE_BORDER = 2;
        public const int MAX_LAYERS = 256;
        private ComputeShader shader;
        private DropdownField loadModeField;
        private VisualElement generationValues;
        private IntegerField generationWidth;
        private IntegerField generationHeight;
        private Button templateGenerationButton;
        private ObjectField loadingTemplateField;
        private Toggle toggleBackground;
        private ScrollView segmentList;
        private Button deleteSegmentButton;
        private Button addSegmentButton;
        private Button exportButton;
        private Button saveButton;
        private VisualElement previewTemplate;
        private VisualElement editorPane;
        private TextField segmentName;
        private IntegerField segmentXPos;
        private IntegerField segmentYPos;
        private IntegerField segmentLayer;
        private IntegerField segmentWidth;
        private IntegerField segmentHeight;
        private ObjectField segmentBackground;
        private SliderInt alphaSlider;
        private IntegerField alphaManual;
        private DropdownField horizontalInterpretation;
        private DropdownField verticalInterpretation;
        private SliderInt interpreterAlphaSlider;
        private IntegerField interpreterManualAlpha;
        private ScrollView interpreterView;
        private Button removeInterpretation;
        private Button addInterpretation;
        private TextField interpretationExample;

        Object loadedTemplate;
        Texture2D baseTexture;
        Texture2D preview;
        string outputName;
        List<CardTempSeg> cardSegments = new List<CardTempSeg>();
        int selectedSegment = -1;
        int segmentCounter = 0;
        #endregion

        #region Menu Functions
        [MenuItem("Tools/Card Template Generator")]
        public static void OpenEditorWindow()
        {
            CardTemplateGenerator wnd = GetWindow<CardTemplateGenerator>();
            wnd.titleContent = new GUIContent("Card Template Generator");
            wnd.maxSize = new Vector2(885, 335);
            wnd.minSize = wnd.maxSize;
        }

        private void CreateGUI()
        {
            #region Assign variables
            shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/CardTemplate/Resources/AlphaShader.compute");
            VisualElement root = rootVisualElement;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
               "Assets/Editor/CardTemplate/Elements/CardTemplateGeneratorTree.uxml");
            VisualElement tree = visualTree.Instantiate();
            root.Add(tree);

            loadModeField = root.Q<DropdownField>("load-mode");
            generationValues = root.Q<VisualElement>("generate-new");
            generationWidth = root.Q<IntegerField>("generation-width-field");
            generationHeight = root.Q<IntegerField>("generation-height-field");
            templateGenerationButton = root.Q<Button>("new-template-button");
            loadingTemplateField = root.Q<ObjectField>("loaded-template");
            toggleBackground = root.Q<Toggle>("toggle-background");
            segmentList = root.Q<ScrollView>("segment-list");
            deleteSegmentButton = root.Q<Button>("delete-segment-button");
            addSegmentButton = root.Q<Button>("new-segment-button");
            exportButton = root.Q<Button>("image-export");
            saveButton = root.Q<Button>("save-button");
            previewTemplate = root.Q<VisualElement>("preview-template");
            editorPane = root.Q<VisualElement>("editor-pane");
            segmentName = root.Q<TextField>("segment-name");
            segmentXPos = root.Q<IntegerField>("segment-horizontal");
            segmentYPos = root.Q<IntegerField>("segment-vertical");
            segmentLayer = root.Q<IntegerField>("segment-layer");
            segmentWidth = root.Q<IntegerField>("segment-width");
            segmentHeight = root.Q<IntegerField>("segment-height");
            segmentBackground = root.Q<ObjectField>("segment-background");
            alphaSlider = root.Q<SliderInt>("alpha-slider");
            alphaManual = root.Q<IntegerField>("alpha-manual");
            horizontalInterpretation = root.Q<DropdownField>("interpreter-horizontal");
            verticalInterpretation = root.Q<DropdownField>("interpreter-vertical");
            interpreterAlphaSlider = root.Q<SliderInt>("interpreter-alpha-slider");
            interpreterManualAlpha = root.Q<IntegerField>("interpreter-manual-alpha");
            interpreterView = root.Q<ScrollView>("interpreter");
            removeInterpretation = root.Q<Button>("remove-interpretation");
            addInterpretation = root.Q<Button>("add-interpretation");
            interpretationExample = root.Q<TextField>("interpreter-example");
            #endregion

            #region Register Callbacks
            loadModeField.RegisterValueChangedCallback<string>(LoadModeChanged);
            templateGenerationButton.clicked += GenerateTemplate;
            loadingTemplateField.RegisterValueChangedCallback<Object>(LoadTemplate);
            toggleBackground.RegisterValueChangedCallback<bool>(ToggleBackgroundChanged);
            deleteSegmentButton.clicked += RemoveSegment;
            addSegmentButton.clicked += AddNewSegment;
            exportButton.clicked += ExportImage;
            saveButton.clicked += SaveTemplate;
            segmentName.RegisterValueChangedCallback<string>(SegmentNameChanged);
            segmentXPos.RegisterValueChangedCallback<int>(SegmentXChanged);
            segmentYPos.RegisterValueChangedCallback<int>(SegmentYChanged);
            segmentLayer.RegisterValueChangedCallback<int>(SegmentLayerChanged);
            segmentWidth.RegisterValueChangedCallback<int>(SegmentWidthChanged);
            segmentHeight.RegisterValueChangedCallback<int>(SegmentHeightChanged);
            segmentBackground.RegisterValueChangedCallback<Object>(SegmentBackgroundChanged);
            alphaSlider.RegisterValueChangedCallback<int>(BackgroundAlphaSliderChanged);
            alphaManual.RegisterValueChangedCallback<int>(BackgroundAlphaManuallyChanged);
            horizontalInterpretation.RegisterValueChangedCallback<string>(HorizontalInterPretationChanged);
            verticalInterpretation.RegisterValueChangedCallback<string>(VerticalInterpretationChanged);
            interpreterAlphaSlider.RegisterValueChangedCallback<int>(InterpreterAlphaSliderChanged);
            interpreterManualAlpha.RegisterValueChangedCallback<int>(InterpreterAlphaManuallyChanged);
            removeInterpretation.clicked += RemoveInterpretation;
            addInterpretation.clicked += AddNewInterpretation;
            interpretationExample.RegisterValueChangedCallback<string>(InterpretationExampleChanged);
            #endregion

            LoadModeChanged(null);
            deleteSegmentButton.SetEnabled(false);
            saveButton.SetEnabled(false);
            exportButton.SetEnabled(false);
            addSegmentButton.SetEnabled(false);
            editorPane.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }



        #endregion
        #region Callback Methods
        private void ExportImage()
        {
            var path = EditorUtility.SaveFilePanel(
                "Save Edited Texture",
                Application.dataPath,
                outputName + ".png",
                "png");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            bool resetToggle = toggleBackground.value;
            if(toggleBackground.value)
            {
                toggleBackground.SetValueWithoutNotify(false);
                UpdatePreview();
            }
            byte[] bytes = preview.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            toggleBackground.SetValueWithoutNotify(resetToggle);
            UpdatePreview();

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
        private void ToggleBackgroundChanged(ChangeEvent<bool> evt)
        {
            UpdatePreview();
        }
        private void InterpreterAlphaManuallyChanged(ChangeEvent<int> evt)
        {

            if (evt.newValue > 255)
            {
                interpreterManualAlpha.SetValueWithoutNotify(255);
                interpreterAlphaSlider.SetValueWithoutNotify(255);
            }
            else
            {
                interpreterAlphaSlider.SetValueWithoutNotify(evt.newValue);
            }

            cardSegments[selectedSegment].interpreter_alpha = evt.newValue;
            RebuildCurrentSegment();
        }

        private void InterpreterAlphaSliderChanged(ChangeEvent<int> evt)
        {
            interpreterManualAlpha.SetValueWithoutNotify(evt.newValue);

            cardSegments[selectedSegment].interpreter_alpha = evt.newValue;
            RebuildCurrentSegment();
        }

        private void VerticalInterpretationChanged(ChangeEvent<string> evt)
        {
            cardSegments[selectedSegment].vertical_index = verticalInterpretation.index;
        }

        private void HorizontalInterPretationChanged(ChangeEvent<string> evt)
        {
            cardSegments[selectedSegment].horizontal_index = horizontalInterpretation.index;
        }
        private void InterpretationExampleChanged(ChangeEvent<string> evt)
        {
            cardSegments[selectedSegment].example = interpretationExample.value;
            RebuildCurrentSegment();
        }

        private void AddNewInterpretation()
        {
            VisualElement interpretation_template = new VisualElement();
            interpretation_template.style.flexDirection = FlexDirection.Row;
            TextField key_field = new TextField();
            key_field.label = "";
            key_field.SetValueWithoutNotify("");
            key_field.style.minWidth = 50;
            key_field.RegisterValueChangedCallback<string>(Reinterpret);
            ObjectField object_field = new ObjectField();
            object_field.objectType = typeof(UnityEngine.Texture2D);
            object_field.SetValueWithoutNotify(null);
            object_field.RegisterValueChangedCallback<Object>(Reinterpret);
            interpretation_template.Add(key_field);
            interpretation_template.Add(object_field);
            interpreterView.Add(interpretation_template);
            SegInterElem newInterpretation = new SegInterElem();
            newInterpretation.text_key = key_field.value;
            newInterpretation.answer_tex = object_field.value as Texture2D;
            cardSegments[selectedSegment].interpreter.Add(newInterpretation);
            removeInterpretation.SetEnabled(true);
        }

        private void AddNewInterpretation(string text_key, Texture2D answerTexture, bool load)
        {
            VisualElement interpretation_template = new VisualElement();
            interpretation_template.style.flexDirection = FlexDirection.Row;
            TextField key_field = new TextField();
            key_field.label = "";
            key_field.SetValueWithoutNotify(text_key);
            key_field.RegisterValueChangedCallback<string>(Reinterpret);
            ObjectField object_field = new ObjectField();
            object_field.objectType = typeof(UnityEngine.Texture2D);
            object_field.SetValueWithoutNotify(answerTexture);
            object_field.RegisterValueChangedCallback<Object>(Reinterpret);
            interpretation_template.Add(key_field);
            interpretation_template.Add(object_field);
            interpreterView.Add(interpretation_template);
            SegInterElem newInterpretation = new SegInterElem();
            newInterpretation.text_key = key_field.value;
            newInterpretation.answer_tex = object_field.value as Texture2D;
            if(!load)
            {
                cardSegments[selectedSegment].interpreter.Add(newInterpretation);
            }
            removeInterpretation.SetEnabled(true);
        }

        private void Reinterpret(ChangeEvent<Object> evt)
        {
            Reinterpret();
        }

        private void Reinterpret(ChangeEvent<string> evt)
        {
            Reinterpret();
        }

        private void Reinterpret()
        {
            for (int i = 0; i < cardSegments[selectedSegment].interpreter.Count; i++)
            {
                cardSegments[selectedSegment].interpreter[i].text_key = interpreterView.contentContainer.ElementAt(i).Q<TextField>().value;
                cardSegments[selectedSegment].interpreter[i].answer_tex = interpreterView.contentContainer.ElementAt(i).Q<ObjectField>().value as Texture2D;
            }
            RebuildCurrentSegment();
        }

        private void RemoveInterpretation()
        {
            cardSegments[selectedSegment].interpreter.Remove(cardSegments[selectedSegment].interpreter[cardSegments[selectedSegment].interpreter.Count - 1]);
            interpreterView.contentContainer.RemoveAt(interpreterView.contentContainer.childCount - 1);
            RebuildCurrentSegment();
        }

        private void BackgroundAlphaManuallyChanged(ChangeEvent<int> evt)
        {
            if (evt.newValue > 255)
            {
                alphaManual.SetValueWithoutNotify(255);
                alphaSlider.SetValueWithoutNotify(255);
            }
            else
            {
                alphaSlider.SetValueWithoutNotify(evt.newValue);
            }
            
            cardSegments[selectedSegment].background_alpha = evt.newValue;
            RebuildCurrentSegment();
        }

        private void BackgroundAlphaSliderChanged(ChangeEvent<int> evt)
        {
            alphaManual.SetValueWithoutNotify(evt.newValue);
            cardSegments[selectedSegment].background_alpha = evt.newValue;
            RebuildCurrentSegment();
        }

        private void SegmentBackgroundChanged(ChangeEvent<Object> evt)
        {
            if (evt.newValue == null)
            {
                for (int i = 0; i < cardSegments[selectedSegment].background_img.width; i++)
                {
                    for (int j = 0; j < cardSegments[selectedSegment].background_img.height; j++)
                    {
                        cardSegments[selectedSegment].background_img.SetPixel(i, j, Color.clear);
                    }
                }
            }
            else
            {
                cardSegments[selectedSegment].background_img = evt.newValue as Texture2D;
            }
            cardSegments[selectedSegment].background_img.Apply();
            RebuildCurrentSegment();
        }

        private void SegmentHeightChanged(ChangeEvent<int> evt)
        {
            cardSegments[selectedSegment].size.y = evt.newValue;
            RebuildCurrentSegment();
        }

        private void SegmentWidthChanged(ChangeEvent<int> evt)
        {
            cardSegments[selectedSegment].size.x = evt.newValue;
            RebuildCurrentSegment();
        }

        private void SegmentLayerChanged(ChangeEvent<int> evt)
        {
            if(evt.newValue<MAX_LAYERS)
            {
                cardSegments[selectedSegment].layer = evt.newValue;
            }
            else
            {
                cardSegments[selectedSegment].layer = MAX_LAYERS - 1;
                segmentLayer.SetValueWithoutNotify(MAX_LAYERS - 1);
            }
            cardSegments[selectedSegment].layer = evt.newValue;
            RebuildCurrentSegment();
        }

        private void SegmentYChanged(ChangeEvent<int> evt)
        {
            cardSegments[selectedSegment].position.y = evt.newValue;
            RebuildCurrentSegment();
        }

        private void SegmentXChanged(ChangeEvent<int> evt)
        {
            cardSegments[selectedSegment].position.x = evt.newValue;
            RebuildCurrentSegment();
        }

        private void SegmentNameChanged(ChangeEvent<string> evt)
        {
            cardSegments[selectedSegment].name = evt.newValue;
            segmentList.contentContainer.ElementAt(selectedSegment).Q<RadioButton>().text = evt.newValue;
        }

        private void SaveTemplate()
        {
            int textureIndex = 0;
            var path = EditorUtility.SaveFilePanel(
                "Save Edited Texture",
                Application.dataPath,
                outputName + ".ctg",
                "ctg");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string pathString = path;
            int assetIndex = pathString.IndexOf("Assets", StringComparison.Ordinal);
            string filePath = pathString.Substring(assetIndex, path.Length - assetIndex);
            var pathForTex = Path.GetDirectoryName(path)+"\\"+Path.GetFileNameWithoutExtension(path) + "\\"+"Tex";
            Directory.CreateDirectory(Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path));
            string outputString = (baseTexture.width-2*BASE_BORDER).ToString() + ";" + (baseTexture.height-2*BASE_BORDER).ToString()+"\n";
            foreach (var cardSegment in cardSegments)
            {
                outputString += cardSegment.name + ": " + "\n";
                outputString += cardSegment.position.x.ToString() + ";" + cardSegment.position.y.ToString() + "\n";
                outputString += cardSegment.size.x.ToString() + ";" + cardSegment.size.y.ToString() + "\n";
                outputString += cardSegment.layer.ToString() + "\n";
                outputString += textureIndex.ToString() + "\n";
                string texPathString = pathForTex + textureIndex.ToString() + ".png";
                File.WriteAllBytes(texPathString, cardSegment.background_img.EncodeToPNG());
                string texFilePath = texPathString.Substring(assetIndex, texPathString.Length - assetIndex);
                AssetDatabase.ImportAsset(texFilePath);
                textureIndex++;
                outputString += cardSegment.background_alpha.ToString() + "\n";
                outputString += cardSegment.interpreter_alpha.ToString() + "\n";
                foreach(var stringInterpretation in cardSegment.interpreter)
                {
                    outputString += stringInterpretation.text_key + ">" + textureIndex.ToString() + "\n";
                    texPathString = pathForTex + textureIndex.ToString() + ".png";
                    File.WriteAllBytes(texPathString, stringInterpretation.answer_tex.EncodeToPNG());
                    texFilePath = texPathString.Substring(assetIndex, texPathString.Length - assetIndex);
                    AssetDatabase.ImportAsset(texFilePath);
                    textureIndex++;
                }
            }
            File.WriteAllText(path, outputString);
            AssetDatabase.ImportAsset(filePath);
            string listFilepath = Path.GetDirectoryName(path) + "\\" + Path.GetFileNameWithoutExtension(path) + "List.txt";
            string exampleForList = "First\n";
            for(int i = 0; i<cardSegments.Count;i++)
            {
                exampleForList += cardSegments[i].name + " Element Goes Here\n";
            }
            exampleForList += "Second\n";
            for (int i = 0; i < cardSegments.Count; i++)
            {
                exampleForList += cardSegments[i].name + " Element Goes Here\n";
            }
            File.WriteAllText(listFilepath, exampleForList);
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
        }

        private void AddNewSegment()
        {
            CreateNewSegment();
            UpdatePreview();
        }

        private void RemoveSegment()
        {
            cardSegments.Remove(cardSegments[selectedSegment]);
            segmentList.Remove(segmentList.ElementAt(selectedSegment));
            selectedSegment = -1;
            deleteSegmentButton.SetEnabled(false);
            editorPane.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            UpdatePreview();
        }

        private void LoadTemplate(ChangeEvent<Object> evt)
        {
            throw new NotImplementedException();
        }

        private void GenerateTemplate()
        {
            int texWidth = generationWidth.value + BASE_BORDER * 2;
            int texHeight = generationHeight.value + BASE_BORDER * 2;
            baseTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            for (int i = 0; i < texWidth; i++)
            {
                for (int j = 0; j < texHeight; j++)
                {
                    if (i < BASE_BORDER || j < BASE_BORDER || i > generationWidth.value || j > generationHeight.value)
                    {
                        baseTexture.SetPixel(i, j, Color.red);
                    }
                    else
                    {
                        baseTexture.SetPixel(i, j, Color.black);
                    }
                }
            }
            baseTexture.Apply();
            cardSegments.Clear();
            interpreterView.Clear();
            segmentList.Clear();
            outputName = "NewCardTemplate";
            ScaleToDisplay(texWidth, texHeight);
            CreateNewSegment();

            UpdatePreview();
            saveButton.SetEnabled(true);
            exportButton.SetEnabled(true);
            addSegmentButton.SetEnabled(true);
        }

        private void LoadModeChanged(ChangeEvent<string> evt)
        {
            switch (loadModeField.index)
            {
                case 0:
                    generationValues.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    loadingTemplateField.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    break;
                case 1:
                    generationValues.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    loadingTemplateField.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    break;
            }
            loadedTemplate = null;
            previewTemplate.style.backgroundImage = null;
            baseTexture = null;
            cardSegments.Clear();
            interpreterView.Clear();
            segmentList.Clear();
            //implement a reset?
        }
        #endregion

        #region Logical Functions
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
            previewTemplate.style.width = 250 * xRatio;
            previewTemplate.style.height = 250 * yRatio;
        }

        private void RebuildCurrentSegment()
        {
            cardSegments[selectedSegment].render_texture = TexOpacity(cardSegments[selectedSegment].background_img, cardSegments[selectedSegment].position, cardSegments[selectedSegment].size, (float)cardSegments[selectedSegment].background_alpha / 255f);
            cardSegments[selectedSegment].render_texture.Apply();
            if (cardSegments[selectedSegment].example != string.Empty && cardSegments[selectedSegment].example != "" && cardSegments[selectedSegment].interpreter.Count > 0)
            {
                Vector2Int offset = new Vector2Int(0,0);
                foreach (var decal in InterpretTheString(cardSegments[selectedSegment].example, cardSegments[selectedSegment].interpreter))
                {
                    Texture2D decalTexture = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
                    decalTexture = TexOpacity(cardSegments[selectedSegment].interpreter[decal].answer_tex, cardSegments[selectedSegment].position, cardSegments[selectedSegment].size, offset, cardSegments[selectedSegment].interpreter_alpha / 255f);
                    decalTexture.Apply();

                    cardSegments[selectedSegment].render_texture = WeCooking(cardSegments[selectedSegment].render_texture, decalTexture);
                    cardSegments[selectedSegment].render_texture.Apply();

                    offset.x += cardSegments[selectedSegment].interpreter[decal].answer_tex.width;
                }
            }
            UpdatePreview();
        }

        public List<int> InterpretTheString(string targetText, List<SegInterElem> keys)
        {
            List<int> answer = new List<int>();
            List<Vector2Int> found = new List<Vector2Int>();
            bool key_hit = true;
            for(int h = 0; h<keys.Count;h++)
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
                        Vector2Int record_result = new Vector2Int(h,j);
                        found.Add(record_result);
                    }
                    else
                    {
                        key_hit = true;
                    }
                }
            }
            Vector2Int record_comparison = new Vector2Int(0,targetText.Length+1);
            bool[] handled = new bool[found.Count];
            for(int i = 0; i < found.Count; i++)
            {
                for(int j = 0; j < found.Count; j++)
                {
                    if(!handled[j] && record_comparison.y>found[j].y)
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

        private void UpdatePreview()
        {
            if (baseTexture == null)
            {
                saveButton.SetEnabled(false);
                exportButton.SetEnabled(false);
                addSegmentButton.SetEnabled(false);
                return;
            }
            saveButton.SetEnabled(true);
            exportButton.SetEnabled(true);
            addSegmentButton.SetEnabled(true);

            for(int i = 0; i< cardSegments.Count;i++)
            {
                cardSegments[i].drawn_preview = false;
            }

            if (toggleBackground.value)
            {
                preview = baseTexture;
            }
            else
            {
                preview = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
                for (int i = 0; i < baseTexture.width; i++)
                {
                    for (int j = 0; j < baseTexture.height; j++)
                    {
                        preview.SetPixel(i, j, Color.clear);
                    }
                }
            }
            preview.Apply();
            
            Vector2Int target = new Vector2Int(MAX_LAYERS + 1, 0);
            for (int i = 0; i < cardSegments.Count; i++)
            {
                for(int j = 0; j < cardSegments.Count; j++)
                {
                    if(!cardSegments[j].drawn_preview && cardSegments[j].layer<target.x)
                    {
                        target.x = cardSegments[j].layer;
                        target.y = j;
                    }
                }
                cardSegments[target.y].drawn_preview = true;
                preview = WeCooking(preview, cardSegments[target.y].render_texture);
                preview.Apply();
                target.x = MAX_LAYERS + 1;
            }

            previewTemplate.style.backgroundImage = preview;
        }

        private void CreateNewSegment()
        {
            CardTempSeg newSegment = new CardTempSeg();
            InitSegment(newSegment, segmentCounter);
            cardSegments.Add(newSegment);
            RadioButton newRadioButton = new RadioButton();
            newRadioButton.text = "Segment" + segmentCounter;
            newRadioButton.name = "segment-radio-" + segmentCounter;
            newRadioButton.RegisterValueChangedCallback<bool>(ChangeSelectedSegment);
            segmentList.Add(newRadioButton);
            segmentCounter++;
        }

        private void ChangeSelectedSegment(ChangeEvent<bool> evt)
        {
            int answer = 0;
            foreach (var segmentChild in segmentList.contentContainer.Children())
            {
                if(segmentChild.Q<RadioButton>().value)
                {
                    selectedSegment = answer;
                    deleteSegmentButton.SetEnabled(true);
                    editorPane.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    displaySegmentData();
                    break;
                }
                answer++;
            }
        }

        private void displaySegmentData()
        {
            segmentName.SetValueWithoutNotify(cardSegments[selectedSegment].name);
            segmentXPos.SetValueWithoutNotify(cardSegments[selectedSegment].position.x);
            segmentYPos.SetValueWithoutNotify(cardSegments[selectedSegment].position.y);
            segmentWidth.SetValueWithoutNotify(cardSegments[selectedSegment].size.x);
            segmentHeight.SetValueWithoutNotify(cardSegments[selectedSegment].size.y);
            segmentLayer.SetValueWithoutNotify(cardSegments[selectedSegment].layer);
            segmentBackground.SetValueWithoutNotify(cardSegments[selectedSegment].background_img);
            alphaSlider.SetValueWithoutNotify(cardSegments[selectedSegment].background_alpha);
            alphaManual.SetValueWithoutNotify(cardSegments[selectedSegment].background_alpha);
            horizontalInterpretation.index = (cardSegments[selectedSegment].horizontal_index);
            verticalInterpretation.index = (cardSegments[selectedSegment].vertical_index);
            interpreterAlphaSlider.SetValueWithoutNotify(cardSegments[selectedSegment].interpreter_alpha);
            interpreterManualAlpha.SetValueWithoutNotify(cardSegments[selectedSegment].interpreter_alpha);
            interpreterView.Clear();
            if(cardSegments[selectedSegment].interpreter.Count>0)
            {
                removeInterpretation.SetEnabled(true);
                foreach(var interpretation in cardSegments[selectedSegment].interpreter)
                {
                    AddNewInterpretation(interpretation.text_key, interpretation.answer_tex, true);
                }
            }
            else
            {
                removeInterpretation.SetEnabled(false);
            }
            interpretationExample.SetValueWithoutNotify(cardSegments[selectedSegment].example);
        }

        private void InitSegment(CardTempSeg target, int id)
        {
            target.name = "Segment" + id.ToString();
            target.position = new Vector2Int(0, 0);
            target.size = new Vector2Int(10, 10);
            target.background_img = new Texture2D(target.size.x, target.size.y, TextureFormat.RGBA32, false);
            for (int i = 0; i < target.size.x; i++)
            {
                for (int j = 0; j < target.size.y; j++)
                {
                    target.background_img.SetPixel(i, j, Color.white);
                }
            }
            target.background_img.Apply();
            target.background_alpha = 255;
            target.horizontal_index = 0;
            target.vertical_index = 1;
            target.interpreter_alpha = 255;
            target.example = "";
            target.drawn_preview = false;
            target.render_texture = TexOpacity(target.background_img, target.position, target.size, (float)target.background_alpha / 255.0f);
            target.render_texture.Apply();
        }

        private Texture2D WeCooking(Texture2D inputTexture, Texture2D cookingTexture)
        {
            int kernelHandle = shader.FindKernel("CookOnTop");
            Texture2D selectedTex = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(baseTexture.width, baseTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(baseTexture.width, baseTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();
            RenderTexture cookingTex = new RenderTexture(baseTexture.width, baseTexture.height, 32);
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
            Texture2D selectedTex = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(baseTexture.width, baseTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(baseTexture.width, baseTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();
            
            for(int i = 0; i< baseTexture.width; i++)
            {
                for (int j = 0; j < baseTexture.height; j++)
                {
                    selectedTex.SetPixel(i, j, Color.clear);
                }
            }
            for (int i = 0; i < size.x; i++)
            {
                for (int j = 0; j < size.y; j++)
                {
                    selectedTex.SetPixel(i+BASE_BORDER+location.x, j+BASE_BORDER+location.y, inputTexture.GetPixel(i,j));
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
            Texture2D selectedTex = new Texture2D(baseTexture.width, baseTexture.height, TextureFormat.RGBA32, false);
            RenderTexture resultTex = new RenderTexture(baseTexture.width, baseTexture.height, 32);
            resultTex.enableRandomWrite = true;
            resultTex.Create();
            RenderTexture inputTex = new RenderTexture(baseTexture.width, baseTexture.height, 32);
            inputTex.enableRandomWrite = true;
            inputTex.Create();

            for (int i = 0; i < baseTexture.width; i++)
            {
                for (int j = 0; j < baseTexture.height; j++)
                {
                    selectedTex.SetPixel(i, j, Color.clear);
                }
            }
            for (int i = offset.x; i < size.x && i-offset.x<inputTexture.width; i++)
            {
                for (int j = offset.y; j < size.y && j-offset.y<inputTexture.height; j++)
                {
                    selectedTex.SetPixel(i + BASE_BORDER + location.x, j + BASE_BORDER + location.y, inputTexture.GetPixel(i-offset.x, j-offset.y));
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

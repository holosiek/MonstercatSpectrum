using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using Un4seen.Bass;
using B83.Win32;
using System.Linq;

public class MusicSpectrum : MonoBehaviour {
    //------------------------------------------------------
    // PRIVATE
    //------------------------------------------------------
    // Bars height factor
    private const float BARS_HEIGHT = 400.0f;
    // Amount of bars to draw
    private const int BARS_AMOUNT = 62;
    // Bars smoothing factor used for calculating avarage of given frequancy
    private const int BARS_SMOOTH_FACTOR = 12;
    //------------------------------------------------------
    // Default volume of channel
    private const float DEF_CHANNEL_VOL = 0.2f;
    // Default channel of audio device
    private const int DEF_CHANNEL = -1;
    // Default audio frequancy
    private const int DEF_FREQ = 44100;
    // Default amount of values in "table of values"
    private const int DEF_MAXVALUES = 1000;
    //------------------------------------------------------
    // Max FPS allowed
    private const int FPS_MAX = 144;
    //------------------------------------------------------
    // Bars rectTransform used to change their size
    private RectTransform[] objects = new RectTransform[BARS_AMOUNT];
    // "Table-of-values" consisting of precalculated hights of bars
    private float[] barCalc = new float[DEF_MAXVALUES+1];
    // FFT of stream
    private float[] fft = new float[2048];
    // Bars newest values
    private float[] bars = new float[62];
    // Bars prev values
    private float[,] smoothingBars = new float[BARS_AMOUNT, BARS_SMOOTH_FACTOR];
    // Bass stream value
    private int stream;
    
    //------------------------------------------------------
    // PUBLIC
    //------------------------------------------------------
    [Header("BASS secret keys")]
    public String BassAPIMail;
    public String BassAPIKey;
    [Header("Text display")]
    public UnityEngine.UI.Text artistText;
    public UnityEngine.UI.Text titleText;
    [Header("GUI elements")]
    public GameObject barsObject;
    public GameObject barsContainer;
    public GameObject albumCover;
    public GameObject GUICanvas;
    public Material skyBox;
    
    //------------------------------------------------------
    // METHODS
    //------------------------------------------------------
    // Find avarage color of album cover
    private void AverageColor(){
        // Get texture
        Texture2D tex = albumCover.GetComponent<Renderer>().material.mainTexture as Texture2D;
        // Get pixels of texture
        Color32[] texColors = tex.GetPixels32();
        // Amount of pixels in texture
        int total = texColors.Length;
        // RGB channels, we gonna save here sum of pixels and get later avarage
        long r = 0, g = 0, b = 0;
        // Add every pixel
        for(int i=0; i<total; i++){
            r += texColors[i].r;
            g += texColors[i].g;
            b += texColors[i].b;
        }
        // Set color of skybox according to avarage color
        skyBox.SetColor("ColorT", new Color((float)(r/255.0f)/total, (float)(g/255.0f)/total, (float)(b/255.0f)/total, 1.0f));
    }
    
    //------------------------------------------------------
    // Generate values to "array of values"
    private void calculateValues(){
        // Go through each value and calculate it
        for(int i=0; i<DEF_MAXVALUES+1; i++){
            barCalc[i] = Mathf.Sqrt((float)i/(float)DEF_MAXVALUES);
        }
    }
    
    //------------------------------------------------------
    // Play song
    void playSong(string a_Str){
        /*
        FOR FUTURE USE:
        DirectoryInfo dic = new DirectoryInfo(Path.GetDirectoryName(a_Str));
        artistText.text = "";
        foreach(FileInfo file in dic.GetFiles()){
            artistText.text += file + "\n";
        };
        */
        // Check if file is mp3
        if(Path.GetExtension(a_Str) == ".mp3"){
            // Decomposite filename to parts, which holds artist and title in format ARTIST - TITLE
            var decompressTitle = Path.GetFileNameWithoutExtension(a_Str).Split('-');
            if(decompressTitle.Length > 1){
                // Trim additional spaces
                artistText.text = decompressTitle[0].Trim(' ');
                titleText.text = decompressTitle[1].Trim(' ');
            } else {
                // If ARTIST - TITLE format not found, use filename as name
                artistText.text = Path.GetFileNameWithoutExtension(a_Str);
            }
            
            // Create TagLib's file to get album cover
            var file = TagLib.File.Create(a_Str);
            // If any cover exists
            if(file.Tag.Pictures.Length > 0){
                // Create texture and load album cover to it
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(file.Tag.Pictures[0].Data.Data);
                // Set new album cover image
                albumCover.GetComponent<Renderer>().material.mainTexture = tex;
                // Get avarage colors after loading
                AverageColor();
            }
            
            // Free stream from prev input
            Bass.BASS_StreamFree(stream);
            // Load new stream from file
            stream = Bass.BASS_StreamCreateFile(a_Str, 0, 0, 0);
            // If stream is not null, play it
            if(stream != 0){
                Bass.BASS_ChannelPlay(stream, false);
            }
            // Set value of stream volume
            Bass.BASS_ChannelSetAttribute(stream, BASSAttribute.BASS_ATTRIB_VOL, DEF_CHANNEL_VOL);
        }
    }

    //------------------------------------------------------
    // EVENTS
    //------------------------------------------------------
    // Update every frame
    void Update(){
        // If stream is playing
        if(stream != 0){
            // Get channel data from stream
            Bass.BASS_ChannelGetData(stream, fft, (int)BASSData.BASS_DATA_FFT4096);
            // For each bar calculate new size
            for(int i=0; i<BARS_AMOUNT; i++){
                // Add each prev value of bar and calculate avarage for smoothing
                float sum = 0;
                for(int j=0; j<BARS_SMOOTH_FACTOR-1; j++){
                    smoothingBars[i, j] = smoothingBars[i, j+1];
                    sum += smoothingBars[i,j];
                }
                smoothingBars[i, BARS_SMOOTH_FACTOR-1] = fft[i];
                sum += fft[i];
                // Set value of bar according to table of values
                bars[i] = barCalc[(int)Mathf.Floor(Mathf.Clamp(sum/BARS_SMOOTH_FACTOR, 0.00f, 1.0f)*1000)];
            }
            // For each bar apply new size
            for(int i=0; i<BARS_AMOUNT; i++){
                // Take avarage of bar values
                float barHeight = (bars[Mathf.Clamp(i-1, 0, BARS_AMOUNT-1)] + bars[i] + bars[Mathf.Clamp(i+1, 0, BARS_AMOUNT-1)])/3;
                // Apply new size and position
                objects[i].sizeDelta = new Vector2(objects[i].rect.width, Mathf.Clamp(barHeight, 0.01f, 1.0f)*BARS_HEIGHT);
                objects[i].anchoredPosition = new Vector3(objects[i].anchoredPosition.x, 0.5f+objects[i].rect.height/2, 0.0f);
            }
        }
    }
    
    
    //------------------------------------------------------
    // Awake once
    void Awake(){
        // Precalculate values for "Table-of-values"
        calculateValues();
        // Set target FPS
        Application.targetFrameRate = FPS_MAX;
        // Register Bass with secret keys
        Un4seen.Bass.BassNet.Registration(BassAPIMail, BassAPIKey);
        // Initialize bass
        if(Bass.BASS_Init(DEF_CHANNEL, DEF_FREQ, BASSInit.BASS_DEVICE_DEFAULT, System.IntPtr.Zero)){
            // Instance and position bars
            float divPos = (int)(BARS_AMOUNT/2)*-16.0f;
            for(int i=0; i<BARS_AMOUNT; i++){
                GameObject obj = Instantiate(barsObject, GUICanvas.transform);
                objects[i] = obj.GetComponent<RectTransform>();
                objects[i].anchoredPosition = new Vector3(divPos+i*16.0f, 0.0f, 0.0f);
                obj.transform.SetParent(barsContainer.transform);
            }
            playSong("F:\\Music\\Monstercat all tracks\\SLANDER & Crankdat - Kneel Before Me (feat. Asking Alexandria).mp3");
        }
    }

    //------------------------------------------------------
    // On application quit, free bass stream and buffer
    void OnApplicationQuit(){
        Bass.BASS_StreamFree(stream);
        Bass.BASS_Free();
    }
    //------------------------------------------------------
    // Unity hook for drag-and-drop file support
    //------------------------------------------------------
    void OnEnable(){
        UnityDragAndDropHook.InstallHook();
        UnityDragAndDropHook.OnDroppedFiles += OnFiles;
    }
    void OnDisable(){
        UnityDragAndDropHook.UninstallHook();
    }
    void OnFiles(List<string> aFiles, POINT aPos){
        string str = aFiles.Aggregate((a, b) => b);
        // Play song
        playSong(str);
    }
    //------------------------------------------------------
}

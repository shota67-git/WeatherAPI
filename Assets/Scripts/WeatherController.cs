using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// 對應 JSON 最外層。
// 不需要把 API 所有欄位都宣告，只保留會使用的欄位。
[Serializable]
public class WeatherResponse
{
    public CurrentWeather current;
}

[Serializable]
public class CurrentWeather
{
    public string time;
    public int interval;
    public float temperature_2m;
    public float precipitation;
    public int is_day;
}

public class WeatherController : MonoBehaviour
{
    [Header("UI")]
    public TMP_Dropdown cityDropdown;
    public TMP_Text temperatureText;
    public TMP_Text weatherText;
    public TMP_Text timeText;
    public TMP_Text statusText;

    [Header("Buttons")]
    public Button refreshButton;
    public Button testDayButton;
    public Button testRainButton;

    [Header("Scene")]
    public Camera sceneCamera;
    public GameObject sun;
    public GameObject moon;
    public ParticleSystem rain;

    [Header("Settings")]
    public float hotTemperature = 30f;

    private bool isLoading;

    // 保存最近一次成功的線上結果，方便失敗時清楚標示。
    private string lastLiveCity;
    private string lastLiveTime;
    private bool showingLiveData;

    private readonly string[] cityNames =
    {
        "Taipei",
        "Tokyo",
        "London"
    };

    private readonly float[] latitudes =
    {
        25.03f,
        35.68f,
        51.51f
    };

    private readonly float[] longitudes =
    {
        121.56f,
        139.69f,
        -0.13f
    };

    private void Start()
    {
        // 若 Inspector 漏拖物件，直接顯示容易辨認的錯誤。
        if (!ReferencesReady())
        {
            Debug.LogError(
                "WeatherController: Inspector 有欄位未指定，請檢查 UI、Buttons、Scene。"
            );

            enabled = false;
            return;
        }

        cityDropdown.ClearOptions();
        cityDropdown.AddOptions(new List<string>(cityNames));
        cityDropdown.SetValueWithoutNotify(0);

        // 自動綁定按鈕，不需要在 Inspector 設定 On Click。
        refreshButton.onClick.AddListener(RefreshWeather);
        testDayButton.onClick.AddListener(TestDay);
        testRainButton.onClick.AddListener(TestRainNight);

        // 先顯示中性背景，避免未取得資料就假裝是白天。
        sun.SetActive(false);
        moon.SetActive(false);

        rain.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        sceneCamera.backgroundColor =
            new Color(0.20f, 0.25f, 0.32f);

        temperatureText.text = "Temperature: -- C";
        weatherText.text = "Weather: --";
        timeText.text = "Data time: --";

        // 開始遊戲時，自動讀取臺北資料。
        RefreshWeather();
    }

    private bool ReferencesReady()
    {
        return cityDropdown != null
            && temperatureText != null
            && weatherText != null
            && timeText != null
            && statusText != null
            && refreshButton != null
            && testDayButton != null
            && testRainButton != null
            && sceneCamera != null
            && sun != null
            && moon != null
            && rain != null;
    }

    public void RefreshWeather()
    {
        // 避免重複送出請求。
        if (isLoading)
        {
            return;
        }

        int index = cityDropdown.value;
        StartCoroutine(GetWeather(index));
    }

    private IEnumerator GetWeather(int cityIndex)
    {
        SetLoading(true);

        string city = cityNames[cityIndex];

        statusText.color = Color.white;
        statusText.text = "Loading " + city + "...";

        // 確保小數點使用 "."，不受電腦語系影響。
        string latitude = latitudes[cityIndex].ToString(
            CultureInfo.InvariantCulture
        );

        string longitude = longitudes[cityIndex].ToString(
            CultureInfo.InvariantCulture
        );

        string url =
            "https://api.open-meteo.com/v1/forecast"
            + "?latitude=" + latitude
            + "&longitude=" + longitude
            + "&current=temperature_2m,precipitation,is_day"
            + "&timezone=auto";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowError("Connection failed. Try again.");

                Debug.LogWarning(
                    "Weather API request failed: "
                    + request.error
                    + "\nURL: "
                    + url
                );
            }
            else
            {
                try
                {
                    string json = request.downloadHandler.text;

                    Debug.Log("Weather JSON:\n" + json);

                    WeatherResponse response =
                        JsonUtility.FromJson<WeatherResponse>(json);

                    if (response == null
                        || response.current == null
                        || string.IsNullOrEmpty(response.current.time))
                    {
                        ShowError("Weather data is missing.");
                    }
                    else
                    {
                        ApplyWeather(
                            response.current,
                            city,
                            false
                        );

                        lastLiveCity = city;
                        lastLiveTime = response.current.time;
                        showingLiveData = true;

                        statusText.color =
                            new Color(0.45f, 1f, 0.55f);

                        statusText.text = "Updated: " + city;
                    }
                }
                catch (Exception exception)
                {
                    ShowError("Cannot read weather data.");
                    Debug.LogWarning(exception.Message);
                }
            }
        }

        SetLoading(false);
    }

    private void ApplyWeather(
        CurrentWeather data,
        string city,
        bool isTest)
    {
        bool isDay = data.is_day == 1;
        bool hasPrecipitation = data.precipitation > 0f;

        // 更新氣溫。
        temperatureText.text =
            city + ": "
            + data.temperature_2m.ToString("0.0")
            + " C";

        temperatureText.color =
            data.temperature_2m >= hotTemperature
                ? new Color(1f, 0.45f, 0.35f)
                : Color.white;

        // 顯示天氣資料，不把「沒有降水」直接等同於晴天。
        string dayText = isDay ? "Day" : "Night";
        string rainText = hasPrecipitation
            ? "Precipitation"
            : "No precipitation";

        weatherText.text =
            dayText + " / " + rainText
            + "\nPrecipitation: "
            + data.precipitation.ToString("0.0")
            + " mm";

        if (isTest)
        {
            timeText.text = "TEST DATA - not live weather";
        }
        else
        {
            timeText.text =
                "Data time (" + city + " local):\n"
                + data.time.Replace("T", " ");
        }

        // 太陽、月亮只代表日夜。
        sun.SetActive(isDay);
        moon.SetActive(!isDay);

        // 依日夜與降水切換背景。
        if (isDay)
        {
            sceneCamera.backgroundColor = hasPrecipitation
                ? new Color(0.42f, 0.50f, 0.60f)
                : new Color(0.35f, 0.72f, 0.95f);
        }
        else
        {
            sceneCamera.backgroundColor = hasPrecipitation
                ? new Color(0.07f, 0.09f, 0.16f)
                : new Color(0.06f, 0.10f, 0.25f);
        }

        // 所有降水在本課程都用雨滴效果示意。
        if (hasPrecipitation)
        {
            if (!rain.isPlaying)
            {
                rain.Play();
            }
        }
        else
        {
            rain.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    public void TestDay()
    {
        if (isLoading)
        {
            return;
        }

        // 用固定 JSON 測試解析與場景更新。
        string json =
            "{\"current\":{"
            + "\"time\":\"TEST\","
            + "\"interval\":900,"
            + "\"temperature_2m\":32.0,"
            + "\"precipitation\":0.0,"
            + "\"is_day\":1"
            + "}}";

        ApplyTestJson(json, "Test Day");
    }

    public void TestRainNight()
    {
        if (isLoading)
        {
            return;
        }

        string json =
            "{\"current\":{"
            + "\"time\":\"TEST\","
            + "\"interval\":900,"
            + "\"temperature_2m\":18.0,"
            + "\"precipitation\":2.5,"
            + "\"is_day\":0"
            + "}}";

        ApplyTestJson(json, "Test Rain Night");
    }

    private void ApplyTestJson(string json, string testName)
    {
        WeatherResponse response =
            JsonUtility.FromJson<WeatherResponse>(json);

        ApplyWeather(response.current, testName, true);

        showingLiveData = false;

        statusText.color = new Color(1f, 0.85f, 0.3f);
        statusText.text = "Test mode. Get Weather to return.";
    }

    private void SetLoading(bool loading)
    {
        isLoading = loading;

        cityDropdown.interactable = !loading;
        refreshButton.interactable = !loading;
        testDayButton.interactable = !loading;
        testRainButton.interactable = !loading;
    }

    private void ShowError(string message)
    {
        statusText.color = new Color(1f, 0.5f, 0.4f);
        statusText.text = message;

        // 下載失敗時保留舊畫面，但明確標示不是最新結果。
        if (showingLiveData)
        {
            timeText.text =
                "Previous data: " + lastLiveCity + "\n"
                + lastLiveTime.Replace("T", " ");
        }
    }

    private void OnDestroy()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshWeather);
        }

        if (testDayButton != null)
        {
            testDayButton.onClick.RemoveListener(TestDay);
        }

        if (testRainButton != null)
        {
            testRainButton.onClick.RemoveListener(TestRainNight);
        }
    }
}
-- Runtime controls for the shared Castle of Ideas lecture feed.
-- The URL is entered at runtime and is never stored in the project or bundle.

local SCRIPT_VERSION = "0.3.2-debug1"
print("[CastleOfIdeas] bootstrap " .. SCRIPT_VERSION)

local function loadModule(name)
    local ok, moduleOrError = pcall(require, name)
    if ok then
        print("[CastleOfIdeas] module loaded: " .. name)
        return moduleOrError
    end
    print("[CastleOfIdeas] module failed: " .. name .. ": " .. tostring(moduleOrError))
    return nil
end

-- Loading these modules registers the component bindings used below. Keep the
-- calls protected so an unavailable binding is visible in Player.log instead
-- of stopping the script before Start() can report its state.
UnityEngine = loadModule("UnityEngine")
UnityUI = loadModule("UnityEngine.UI")
CCK = loadModule("CVR.CCK")

local player
local urlInput
local volumeSlider
local volumeLabel
local statusLabel
local programMixer
local lastUrl = ""
local lastDb = nil
local lastLoggedDb = nil
local mixerFailureReported = false

local function debugLog(message)
    print("[CastleOfIdeas] " .. tostring(message))
end

local function present(value)
    return value ~= nil and IsValid(value)
end

local function setStatus(message)
    if statusLabel then
        statusLabel.text = message
    end
end

local function isSupportedUrl(url)
    if type(url) ~= "string" then
        return false
    end
    local lower = string.lower(url)
    return string.match(lower, "^https://") ~= nil
        and string.match(lower, "%.m3u8([?#].*)?$") ~= nil
end

local function applyGain(db)
    if not programMixer then
        return false
    end

    local ok, result = pcall(function()
        return programMixer:SetFloat("ProgramGain", db)
    end)
    if not ok and not mixerFailureReported then
        mixerFailureReported = true
        debugLog("AudioMixer.SetFloat unavailable; using player volume fallback: " .. tostring(result))
    end
    return ok and result ~= false
end

function Start()
    debugLog("Start " .. SCRIPT_VERSION)
    player = BoundObjects.ProgramPlayer
    urlInput = BoundObjects.UrlInput
    volumeSlider = BoundObjects.VolumeSlider
    volumeLabel = BoundObjects.VolumeLabel
    statusLabel = BoundObjects.StatusLabel
    programMixer = BoundObjects.ProgramMixer

    debugLog(
        "bindings player=" .. tostring(present(player))
        .. " input=" .. tostring(present(urlInput))
        .. " slider=" .. tostring(present(volumeSlider))
        .. " volumeLabel=" .. tostring(present(volumeLabel))
        .. " statusLabel=" .. tostring(present(statusLabel))
        .. " mixer=" .. tostring(present(programMixer))
    )

    if volumeSlider then
        volumeSlider.value = 0.0
    end
    debugLog("initial 0 dB mixerApplied=" .. tostring(applyGain(0.0)))
    setStatus("受信URLを入力して適用してください")
    debugLog("ready")
end

function Update()
    if not volumeSlider then
        return
    end

    local db = volumeSlider.value
    if lastDb == nil or math.abs(db - lastDb) >= 0.01 then
        if volumeLabel then
            volumeLabel.text = string.format("%+.1f dB", db)
        end
        if not applyGain(db) and player then
            -- Safe fallback for clients that do not expose AudioMixer.SetFloat.
            -- Positive values saturate at the video player's maximum volume.
            player.playbackVolume = math.min(1.0, math.pow(10.0, db / 20.0))
        end
        if lastLoggedDb == nil or math.abs(db - lastLoggedDb) >= 1.0 then
            debugLog("volume changed db=" .. string.format("%.1f", db))
            lastLoggedDb = db
        end
        lastDb = db
    end
end

function ApplyUrl()
    if not player or not urlInput then
        setStatus("Video Playerを初期化できませんでした")
        return
    end

    local url = urlInput.text
    if not isSupportedUrl(url) then
        setStatus("HTTPSの.m3u8受信URLを入力してください")
        debugLog("ApplyUrl rejected input length=" .. tostring(string.len(url or "")))
        return
    end

    lastUrl = url
    local ok, result = pcall(function()
        player:SetUrl(url)
    end)
    if not ok then
        setStatus("URL適用に失敗しました。デバッグログを確認してください")
        debugLog("ApplyUrl SetUrl failed: " .. tostring(result))
        return
    end
    setStatus("再生を要求しました。映らなければ再読込してください")
    debugLog("ApplyUrl SetUrl requested; URL redacted, length=" .. tostring(string.len(url)))
end

function ReloadUrl()
    if not player then
        setStatus("Video Playerを初期化できませんでした")
        return
    end

    local url = lastUrl
    if url == "" and urlInput then
        url = urlInput.text
    end
    if not isSupportedUrl(url) then
        setStatus("先に有効な受信URLを入力してください")
        return
    end

    lastUrl = url
    local ok, result = pcall(function()
        player:SetUrl(url)
    end)
    if not ok then
        setStatus("再読込に失敗しました。デバッグログを確認してください")
        debugLog("ReloadUrl SetUrl failed: " .. tostring(result))
        return
    end
    setStatus("受信URLを再読込しました")
    debugLog("ReloadUrl SetUrl requested; URL redacted, length=" .. tostring(string.len(url)))
end

function DumpDiagnostics()
    local inputLength = 0
    if urlInput and urlInput.text then
        inputLength = string.len(urlInput.text)
    end
    local sliderValue = "missing"
    if volumeSlider then
        sliderValue = string.format("%.1f", volumeSlider.value)
    end
    debugLog(
        "diagnostics version=" .. SCRIPT_VERSION
        .. " player=" .. tostring(present(player))
        .. " input=" .. tostring(present(urlInput))
        .. " slider=" .. tostring(present(volumeSlider))
        .. " mixer=" .. tostring(present(programMixer))
        .. " inputLength=" .. tostring(inputLength)
        .. " cachedUrl=" .. tostring(lastUrl ~= "")
        .. " sliderDb=" .. sliderValue
    )
    setStatus("診断情報を外部ログへ出力しました")
end

-- Runtime controls for the shared Castle of Ideas lecture feed.
-- The URL is entered at runtime and is never stored in the project or bundle.

local player
local urlInput
local volumeSlider
local volumeLabel
local statusLabel
local programMixer
local lastUrl = ""
local lastDb = nil

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
    return ok and result ~= false
end

function Start()
    player = BoundObjects.ProgramPlayer
    urlInput = BoundObjects.UrlInput
    volumeSlider = BoundObjects.VolumeSlider
    volumeLabel = BoundObjects.VolumeLabel
    statusLabel = BoundObjects.StatusLabel
    programMixer = BoundObjects.ProgramMixer

    if volumeSlider then
        volumeSlider.value = 0.0
    end
    applyGain(0.0)
    setStatus("受信URLを入力して適用してください")
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
        return
    end

    lastUrl = url
    player:SetUrl(url)
    setStatus("再生を要求しました。映らなければ再読込してください")
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
    player:SetUrl(url)
    setStatus("受信URLを再読込しました")
end

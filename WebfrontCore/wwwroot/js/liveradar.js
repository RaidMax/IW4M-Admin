const textOffset = 15;
let previousRadarData = undefined;
let newRadarData = undefined;
let stateInfo;

/************************
 *          IW4         *
 * **********************/
const weapons = {};
weapons["ak47"] = "ak47";
weapons["ak47classic"] = "icon_ak47_classic";
weapons["ak74u"] = "akd74u";
weapons["m16"] = "m16a4";
weapons["m4"] = "m4carbine";
weapons["fn2000"] = "fn2000";
weapons["masada"] = "masada";
weapons["famas"] = "famas";
weapons["fal"] = "fnfal";
weapons["scar"] = "scar_h";
weapons["tavor"] = "tavor";

weapons["mp5k"] = "mp5k";
weapons["uzi"] = "mini_uzi";
weapons["p90"] = "p90";
weapons["kriss"] = "kriss";
weapons["ump45"] = "ump45";

weapons["rpd"] = "rpd";
weapons["sa80"] = "sa80_lmg";
weapons["mg4"] = "mg4";
weapons["m240"] = "m240";
weapons["aug"] = "steyr";

weapons["barrett"] = "barrett50cal";
weapons["wa2000"] = "wa2000";
weapons["m21"] = "m14ebr";
weapons["cheytac"] = "cheytac";
weapons["dragunov"] = "dragunovsvd";

weapons["beretta"] = "m9beretta";
weapons["usp"] = "usp_45";
weapons["deserteagle"] = "desert_eagle";
weapons["deserteaglegold"] = "desert_eagle_gold";
weapons["desert"]
weapons["coltanaconda"] = "colt_anaconda";

weapons["tmp"] = "mp9";
weapons["glock"] = "glock";
weapons["beretta393"] = "beretta393";
weapons["pp2000"] = "pp2000";

weapons["ranger"] = "sawed_off";
weapons["model1887"] = "model1887";
weapons["striker"] = "striker";
weapons["aa12"] = "aa12";
weapons["m1014"] = "benelli_m4";
weapons["spas12"] = "spas12";

weapons["m79"] = "m79";
weapons["rpg"] = "rpg";
weapons["at4"] = "at4";
weapons["stinger"] = "stinger";
weapons["javelin"] = "javelin";

weapons["m40a3"] = "m40a3";
weapons["none"] = "neutral";
weapons["riotshield"] = "riot_shield";
weapons["peacekeeper"] = "peacekeeper";

function drawCircle(context, x, y, color) {
    context.beginPath();
    context.arc(x, y, 6 * stateInfo.imageScaler, 0, 2 * Math.PI, false);
    context.fillStyle = color;
    context.fill();
    context.lineWidth = 0.5;
    context.strokeStyle = 'rgba(255, 255, 255, 0.5)';
    context.closePath();
    context.stroke();
}

function drawLine(context, x1, y1, x2, y2, color) {
    context.beginPath();
    context.lineWidth = '3';
    context.moveTo(x1, y1);
    context.lineTo(x2, y2);
    context.closePath();
    context.stroke();
}

function drawTriangle(context, v1, v2, v3, color) {
    context.beginPath();
    context.moveTo(v1.x, v1.y);
    context.lineTo(v2.x, v2.y);
    context.lineTo(v3.x, v3.y);
    context.closePath();
    context.fillStyle = color;
    context.fill();
}

function drawText(context, x, y, text, size, fillColor, strokeColor, alignment = 'left') {
    context.beginPath();
    context.save();
    context.font = `bold ${Math.max(12, size * stateInfo.imageScaler)}px courier new`;
    context.fillStyle = fillColor;
    context.shadowColor = strokeColor;
    context.shadowBlur = 4;
    context.textAlign = alignment;
    context.fillText(text, x, y);
    context.restore();
    context.closePath();
}

function drawImage(context, imgSelector, x, y, alpha = 1) {
    context.save();
    context.globalAlpha = alpha;
    context.drawImage(document.getElementById(imgSelector), x - (15 * stateInfo.imageScaler), y - (15 * stateInfo.imageScaler), 32 * stateInfo.imageScaler, 32 * stateInfo.imageScaler);
    context.globalAlpha = 1;
    context.restore();
}

function checkCanvasSize(canvas, context, minimap, map) {

    let width = Math.round(minimap.width());
    if (Math.round(context.canvas.width) !== width) {

        canvas.width(width);
        canvas.height(width);

        context.canvas.height = width;
        context.canvas.width = context.canvas.height;
    }

    stateInfo.imageScaler = (stateInfo.canvas.width() / 1024)
    stateInfo.mapScalerX = (((stateInfo.mapInfo.right * stateInfo.imageScaler) - (stateInfo.mapInfo.left * stateInfo.imageScaler)) / stateInfo.mapInfo.width);
    stateInfo.mapScalerY = (((stateInfo.mapInfo.bottom * stateInfo.imageScaler) - (stateInfo.mapInfo.top * stateInfo.imageScaler)) / stateInfo.mapInfo.height);
    stateInfo.mapScaler = (stateInfo.mapScalerX + stateInfo.mapScalerY) / 2

    stateInfo.forwardDistance = 500.0;
    stateInfo.fovWidth = 40;
}

function calculateViewPosition(x, y, distance) {
    let nx = Math.cos(x) * Math.cos(y);
    let ny = Math.sin(x) * Math.cos(y);
    let nz = Math.sin(360.0 - y);

    return {
        x: (nx * distance) * stateInfo.mapScaler,
        y: (ny * distance) * stateInfo.mapScaler,
        z: (nz * distance) * stateInfo.mapScaler
    };
}

function lerp(start, end, complete) {
    return (1 - complete) * start + complete * end;
}

function easeLerp(start, end, t) {
    let t2 = (1 - Math.cos(t * Math.PI)) / 2;

    return (start * (1 - t2) + end * t2);
}

function fixRollAngles(oldAngles, newAngles) {
    let newX = newAngles.x;
    let newY = newAngles.y;

    let angleDifferenceX = (oldAngles.x - newAngles.x);

    if (angleDifferenceX > Math.PI) {
        newX = oldAngles.x + (Math.PI * 2) - angleDifferenceX;
    } else if (Math.abs(newAngles.x - oldAngles.x) > Math.PI) {
        newX = newAngles.x - (Math.PI * 2);
    }

    let angleDifferenceY = (oldAngles.y - newAngles.y);

    if (angleDifferenceY > Math.PI) {
        newY = oldAngles.y + (Math.PI * 2) - angleDifferenceY;
    } else if (Math.abs(newAngles.y - oldAngles.y) > Math.PI) {
        newY = newAngles.y - (Math.PI * 2);
    }

    return { x: newX, y: newY };
}

function toRadians(deg) {
    return deg * Math.PI / 180.0;
}

function rotate(cx, cy, x, y, angle) {
    var radians = (Math.PI / 180) * angle,
        cos = Math.cos(radians),
        sin = Math.sin(radians),
        nx = (cos * (x - cx)) + (sin * (y - cy)) + cx,
        ny = (cos * (y - cy)) - (sin * (x - cx)) + cy;
    return {
        x: nx,
        y: ny
    };
}

function weaponImageForWeapon(weapon) {
    let name = weapon.split('_')[0];
    if (weapons[name] === undefined) {
        console.log(name);
        name = "none";
    }

    return `/images/radar/hud_weapons/hud_${weapons[name]}.png`;
}

function updatePlayerData() {
    $('.player-data-left').html('');
    $('.player-data-right').html('');

    $.each(newRadarData, function (index, player) {
        if (player == null) {
            return;
        }

        let column = player.team === 'allies' ? $('.player-data-left') : $('.player-data-right');

        let greenProgressClass = 'rounded-top';
        let redProgressClass = 'rounded-right';

        if (player.health < 100) {
            greenProgressClass = 'rounded-left';
        }
        if (player.health <= 0) {
            redProgressClass = 'rounded-top';
        }

        column.append(`
<div class="bg-surface rounded-lg border border-line shadow-sm mb-4 overflow-hidden group hover:border-primary/50 transition-colors">
    <div class="relative h-6 w-full bg-surface-alt">
        <div class="absolute inset-0 flex">
             <div class="h-full bg-emerald-500/80 transition-all duration-300" style="width: ${player.health}%"></div>
             <div class="h-full bg-red-500/80 transition-all duration-300" style="width: ${100 - player.health}%"></div>
        </div>
        <div class="absolute inset-0 flex items-center px-2 text-xs font-bold text-shadow-sm text-white z-10 drop-shadow-md truncate">${player.name}</div>
    </div>
    
    <div class="p-2 flex items-center justify-between text-xs text-foreground/90 bg-surface">
         <div class="w-12 h-6 bg-contain bg-no-repeat bg-left opacity-80" style="background-image:url(${weaponImageForWeapon(player.weapon)})" title="${player.weapon}"></div>
         <div class="flex items-center gap-3">
            <div class="flex items-center gap-1" title="Kills"><i class="ph ph-skull text-muted"></i> <span class="font-mono">${player.kills}</span></div>
            <div class="flex items-center gap-1" title="Deaths"><i class="ph ph-skull text-muted opacity-50"></i> <span class="font-mono">${player.deaths}</span></div>
            <div class="flex items-center gap-1" title="K/D Ratio"><i class="ph ph-crosshair text-muted"></i> <span class="font-mono">${player.deaths == 0 ? player.kills.toFixed(2) : (player.kills / player.deaths).toFixed(2)}</span></div>
            <div class="flex items-center gap-1" title="Score/Min"><i class="ph ph-chart-line-up text-muted"></i> <span class="font-mono">${player.playTime == 0 ? '&mdash;' : Math.round(player.score / (player.playTime / 60))}</span></div>
         </div>
    </div>
</div>`);
    });

    $('.player-data-left').delay(1000).animate({ opacity: 1 }, 500);
    $('.player-data-right').delay(1000).animate({ opacity: 1 }, 500);
}

function updateRadarData() {
    $.getJSON(window.radarDataUrl, function (_radarItem) {
        newRadarData = _radarItem;
    });


    $.getJSON(window.mapDataUrl, function (_map) {
        stateInfo.mapInfo = _map
    });

    $.each(newRadarData, function (index, value) {
        if (previousRadarData !== undefined && index < previousRadarData.length) {

            let previous = previousRadarData[index];

            // this happens when the player has first joined and we haven't gotten two snapshots yet
            if (value == null) {
                return;
            }

            if (previous == null) {
                previous = value;
            }

            // we don't want to treat a disconnected player snapshot as the previous
            else if (previous.guid === value.guid) {
                value.previous = previous;
            }

            // we haven't gotten a new item, it's just the old one again
            if (previous.id === value.id) {
                value.animationTime = previous.animationTime;
                value.previous = value;
            }

            // they died between this snapshot and last so we wanna setup the death icon
            if (!value.isAlive && previous.isAlive) {
                stateInfo.deathIcons[value.guid] = {
                    animationTime: now,
                    location: value.location
                };
            }

            // they respawned between this snapshot and last so we don't want to show wherever the were specating from
            else if (value.isAlive && !previous.isAlive) {
                value.previous = value;
            }
        }
    });

    // we switch out the items to
    previousRadarData = newRadarData;

    $('#map_name').html(stateInfo.mapInfo.alias);
    $('#map_list').css('background-image', `url(/images/radar/minimaps/compass_map_${stateInfo.mapInfo.name}@2x.jpg)`);
    checkCanvasSize(stateInfo.canvas, stateInfo.ctx, $('#map_list'), stateInfo.mapInfo);
    updatePlayerData();
}

function updateMap() {
    let ctx = stateInfo.ctx;

    ctx.clearRect(0, 0, ctx.canvas.width, ctx.canvas.height);
    now = performance.now();

    $.each(previousRadarData, function (index, value) {
        if (value == null) {
            return;
        }

        if (value.previous == null) {
            value.previous = value;
        }

        // this indicates we got a new snapshot to work with so we set the time based off the previous
        // frame deviation to have minimal interpolation skipping
        if (value.animationTime === undefined) {
            value.animationTime = now - stateInfo.updateFrameTimeDeviation;
        }

        if (!value.isAlive) {
            return;
        }

        const elapsedFrameTime = now - value.animationTime;
        const completionPercent = elapsedFrameTime / stateInfo.updateFrequency;

        // certain maps like estate have an off center axis of origin, so we need to account for that
        let rotatedPreviousLocation = rotate(stateInfo.mapInfo.centerX, stateInfo.mapInfo.centerY, value.previous.location.x, value.previous.location.y, stateInfo.mapInfo.rotation);
        let rotatedCurrentLocation = rotate(stateInfo.mapInfo.centerX, stateInfo.mapInfo.centerY, value.location.x, value.location.y, stateInfo.mapInfo.rotation);

        const startX = ((stateInfo.mapInfo.maxLeft - rotatedPreviousLocation.y) * stateInfo.mapScaler) + (stateInfo.mapInfo.left * stateInfo.imageScaler);
        const startY = ((stateInfo.mapInfo.maxTop - rotatedPreviousLocation.x) * stateInfo.mapScalerY) + (stateInfo.mapInfo.top * stateInfo.imageScaler);

        const endX = ((stateInfo.mapInfo.maxLeft - rotatedCurrentLocation.y) * stateInfo.mapScaler) + (stateInfo.mapInfo.left * stateInfo.imageScaler);
        const endY = ((stateInfo.mapInfo.maxTop - rotatedCurrentLocation.x) * stateInfo.mapScalerY) + (stateInfo.mapInfo.top * stateInfo.imageScaler);

        let teamColor = value.team === 'allies' ? 'rgb(0, 122, 204, 1)' : 'rgb(255, 69, 69)';
        let fovColor = value.team === 'allies' ? 'rgba(0, 122, 204, 0.2)' : 'rgba(255, 69, 69, 0.2)';

        // this takes care of moving past the roll-over point of yaw/pitch (ie 360->0)
        const rollAngleFix = fixRollAngles(value.previous.radianAngles, value.radianAngles);

        const radianLerpX = lerp(value.previous.radianAngles.x, rollAngleFix.x, completionPercent);
        const radianLerpY = lerp(value.previous.radianAngles.y, rollAngleFix.y, completionPercent);

        // this is some jankiness to get the fov to point the right direction
        let firstVertex = calculateViewPosition(toRadians(stateInfo.mapInfo.rotation + stateInfo.mapInfo.viewPositionRotation - 90) - radianLerpX + toRadians(stateInfo.fovWidth), radianLerpY, stateInfo.forwardDistance);
        let secondVertex = calculateViewPosition(toRadians(stateInfo.mapInfo.rotation + stateInfo.mapInfo.viewPositionRotation - 90) - radianLerpX - toRadians(stateInfo.fovWidth), radianLerpY, stateInfo.forwardDistance);

        let currentX = lerp(startX, endX, completionPercent);
        let currentY = lerp(startY, endY, completionPercent);

        // we need to calculate the distance from the center of the map so we can scale if necessary
        let centerX = ((stateInfo.mapInfo.maxLeft - stateInfo.mapInfo.centerY) * stateInfo.mapScaler) + (stateInfo.mapInfo.left * stateInfo.imageScaler);
        let centerY = ((stateInfo.mapInfo.maxTop - stateInfo.mapInfo.centerX) * stateInfo.mapScaler) + (stateInfo.mapInfo.top * stateInfo.imageScaler);

        // reuse lerp to scale the pixel to map ratio
        currentX = lerp(centerX, currentX, stateInfo.mapInfo.scaler);
        currentY = lerp(centerY, currentY, stateInfo.mapInfo.scaler);

        drawCircle(ctx, currentX, currentY, teamColor);
        drawTriangle(ctx,
            { x: currentX, y: currentY },
            { x: currentX + firstVertex.x, y: currentY + firstVertex.y },
            { x: currentX + secondVertex.x, y: currentY + secondVertex.y },
            fovColor);
        drawText(ctx, currentX, currentY - (textOffset * stateInfo.imageScaler), value.name, 16, 'white', teamColor, 'center')
    });

    const completedIcons = [];

    for (let key in stateInfo.deathIcons) {
        const icon = stateInfo.deathIcons[key];

        const x = ((stateInfo.mapInfo.maxLeft - icon.location.y) * stateInfo.mapScaler) + (stateInfo.mapInfo.left * stateInfo.imageScaler);
        const y = ((stateInfo.mapInfo.maxTop - icon.location.x) * stateInfo.mapScaler) + (stateInfo.mapInfo.top * stateInfo.imageScaler);

        const elapsedFrameTime = now - icon.animationTime;
        const completionPercent = elapsedFrameTime / stateInfo.deathIconTime;
        const opacity = easeLerp(1, 0, completionPercent);

        drawImage(stateInfo.ctx, 'hud_death', x, y, opacity);

        if (completionPercent >= 1) {
            completedIcons.push(key);
        }
    }

    for (let i = 0; i < completedIcons.length; i++) {
        delete stateInfo.deathIcons[completedIcons[i]];
    }

    window.requestAnimationFrame(updateMap);
}

function initCallOfDutyLiveRadar(radarDataUrl, mapDataUrl) {
    if ($('#map_canvas').length === 0) {
        console.error("[LiveRadar] Canvas #map_canvas not found!");
        return;
    }

    // Reset state if re-initializing
    if (stateInfo && stateInfo.intervalId) {
        console.log("[LiveRadar] Cleaning up previous interval", stateInfo.intervalId);
        clearInterval(stateInfo.intervalId);
    }

    stateInfo = {
        canvas: $('#map_canvas'),
        ctx: $('#map_canvas')[0].getContext('2d'),
        updateFrequency: 750,
        updateFrameTimeDeviation: 0,
        forwardDistance: undefined,
        fovWidth: undefined,
        mapInfo: undefined,
        mapScaler: undefined,
        deathIcons: {},
        deathIconTime: 4000
    };

    // Globals update
    window.radarDataUrl = radarDataUrl;
    window.mapDataUrl = mapDataUrl;

    // Correct logic: First fetch MAP metadata, then start polling for radar entities.
    $.getJSON(window.mapDataUrl, function (_map) {
        stateInfo.mapInfo = _map;

        // Initial Radar Data fetch
        updateRadarData();

        // Start polling
        stateInfo.intervalId = setInterval(updateRadarData, stateInfo.updateFrequency);
        window.requestAnimationFrame(updateMap);
    }).fail(function (jqxhr, textStatus, error) {
        console.error("[LiveRadar] Map Metadata fetch failed:", textStatus, error);
    });
}


(function () {
let radarState;
const tileCacheMilliseconds = 86400000;
const tileCacheVersion = 2;

function escapeText(value) {
    return $('<div>').text(value == null ? '' : String(value)).html();
}
function drawPlayer(ctx, x, y, name) {
    ctx.beginPath();
    ctx.arc(x, y, 6, 0, 2 * Math.PI);
    ctx.fillStyle = 'rgb(0, 204, 136)';
    ctx.fill();
    ctx.lineWidth = 0.5;
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
    ctx.stroke();

    ctx.save();
    ctx.font = 'bold 14px courier new';
    ctx.fillStyle = 'white';
    ctx.shadowColor = 'rgba(0, 0, 0, 0.9)';
    ctx.shadowBlur = 4;
    ctx.textAlign = 'center';
    ctx.fillText(name, x, y - 14);
    ctx.restore();
}

function updatePlayerCards() {
    const players = Array.isArray(radarState.players) ? radarState.players : [];
    const columns = $('.player-data-left, .player-data-left-mobile');
    columns.html('');
    $('.player-data-right, .player-data-right-mobile').html('');

    $.each(players, function (_, player) {
        if (!player) return;
        const health = Math.max(0, Math.min(100, Number(player.health) || 0));
        columns.append(`
<div class="bg-surface rounded-lg border border-line shadow-sm mb-4 overflow-hidden group hover:border-primary/50 transition-colors">
    <div class="relative h-6 w-full bg-surface-alt">
        <div class="absolute inset-y-0 left-0 bg-emerald-500/80 transition-all duration-300" style="width:${health}%"></div>
        <div class="absolute inset-0 flex items-center px-2 text-xs font-bold text-white z-10 drop-shadow-md truncate">${escapeText(player.name)}</div>
    </div>
    <div class="p-2 grid grid-cols-3 gap-2 text-xs text-foreground/90 bg-surface">
        <div title="Player level"><i class="ph ph-arrow-circle-up text-muted"></i> <span class="font-mono">${Number(player.score) || 0}</span></div>
        <div title="Zombie kills"><i class="ph ph-skull text-muted"></i> <span class="font-mono">${Number(player.kills) || 0}</span></div>
        <div title="Deaths"><i class="ph ph-heart-break text-muted"></i> <span class="font-mono">${Number(player.deaths) || 0}</span></div>
    </div>
</div>`);
    });
}

function scaleAtZoom() {
    return Math.pow(2, radarState.zoom - radarState.mapInfo.maxZoom);
}

function clampCenter() {
    const mapSize = radarState.mapInfo.mapSize;
    const scale = scaleAtZoom();
    const limitX = Math.max(0, mapSize.x / 2 - radarState.canvas.width / (2 * scale));
    const limitZ = Math.max(0, mapSize.z / 2 - radarState.canvas.height / (2 * scale));
    radarState.centerX = Math.max(-limitX, Math.min(limitX, radarState.centerX));
    radarState.centerZ = Math.max(-limitZ, Math.min(limitZ, radarState.centerZ));
}

function resizeCanvas() {
    const size = Math.max(320, Math.round($('#map_canvas').width()));
    if (radarState.canvas.width !== size || radarState.canvas.height !== size) {
        radarState.canvas.width = size;
        radarState.canvas.height = size;
        radarState.terrainCanvas.width = size;
        radarState.terrainCanvas.height = size;
        clampCenter();
        radarState.terrainDirty = true;
        return true;
    }
    return false;
}

function queueRender(terrainChanged = false, delay = 0) {
    if (!radarState) return;
    if (terrainChanged) radarState.terrainDirty = true;

    if (delay > 0) {
        clearTimeout(radarState.renderTimer);
        radarState.renderTimer = setTimeout(() => queueRender(terrainChanged), delay);
        return;
    }

    if (radarState.animationFrame) return;
    radarState.animationFrame = requestAnimationFrame(() => {
        radarState.animationFrame = 0;
        drawMap();
    });
}

function getTile(zoom, x, y) {
    const key = `${zoom}:${x}:${y}`;
    const existing = radarState.tiles.get(key);
    const now = Date.now();
    if (existing && (now - existing.loadedAt < tileCacheMilliseconds || existing.refreshing)) {
        return existing;
    }

    const image = new Image();
    if (existing?.failedAt) return existing;

    const tile = existing || {
        image,
        loaded: false,
        loadedAt: 0,
        refreshing: false,
        failedAt: 0,
        retryCount: 0,
        retryTimer: 0
    };
    tile.refreshing = true;
    image.onload = () => {
        clearTimeout(tile.retryTimer);
        tile.image = image;
        tile.loaded = true;
        tile.loadedAt = Date.now();
        tile.refreshing = false;
        tile.failedAt = 0;
        tile.retryCount = 0;
        queueRender(true);
    };
    image.onerror = () => {
        tile.refreshing = false;
        tile.failedAt = Date.now();
        tile.retryCount += 1;
        const retryDelay = Math.min(30000, 1000 * Math.pow(2, Math.min(tile.retryCount - 1, 5)));
        clearTimeout(tile.retryTimer);
        tile.retryTimer = setTimeout(() => {
            if (!radarState || radarState.tiles.get(key) !== tile) return;
            tile.failedAt = 0;
            queueRender(true);
        }, retryDelay);
    };
    image.src = radarState.tileUrl
        .replace('{z}', zoom)
        .replace('{x}', x)
        .replace('{y}', y) + `?v=${tileCacheVersion}-${Math.floor(now / tileCacheMilliseconds)}`;
    radarState.tiles.set(key, tile);
    return tile;
}

function drawMap() {
    if (!radarState) return;
    resizeCanvas();

    const { canvas, ctx, mapInfo } = radarState;
    const scale = scaleAtZoom();

    if (radarState.terrainDirty) {
        radarState.terrainDirty = false;
        const terrainCtx = radarState.terrainCtx;
        const tileWorldSize = mapInfo.tileSize / scale;
        const minWorldX = radarState.centerX - canvas.width / (2 * scale);
        const maxWorldX = radarState.centerX + canvas.width / (2 * scale);
        const minProjectedY = -radarState.centerZ - canvas.height / (2 * scale);
        const maxProjectedY = -radarState.centerZ + canvas.height / (2 * scale);
        const mapMinTileX = Math.floor((-mapInfo.mapSize.x / 2) / tileWorldSize);
        const mapMaxTileX = Math.ceil((mapInfo.mapSize.x / 2) / tileWorldSize) - 1;
        const mapMinLeafY = Math.floor((-mapInfo.mapSize.z / 2) / tileWorldSize);
        const mapMaxLeafY = Math.ceil((mapInfo.mapSize.z / 2) / tileWorldSize) - 1;
        const minTileX = Math.max(mapMinTileX, Math.floor(minWorldX / tileWorldSize));
        const maxTileX = Math.min(mapMaxTileX, Math.floor(maxWorldX / tileWorldSize));
        const minLeafY = Math.max(mapMinLeafY, Math.floor(minProjectedY / tileWorldSize));
        const maxLeafY = Math.min(mapMaxLeafY, Math.floor(maxProjectedY / tileWorldSize));

        terrainCtx.fillStyle = '#151719';
        terrainCtx.fillRect(0, 0, canvas.width, canvas.height);
        for (let tileX = minTileX; tileX <= maxTileX; tileX++) {
            for (let leafY = minLeafY; leafY <= maxLeafY; leafY++) {
                const tile = getTile(radarState.zoom, tileX, -leafY - 1);
                if (!tile.loaded) continue;
                const x = Math.round((tileX * tileWorldSize - radarState.centerX) * scale + canvas.width / 2);
                const y = Math.round((leafY * tileWorldSize + radarState.centerZ) * scale + canvas.height / 2);
                terrainCtx.drawImage(tile.image, x, y, mapInfo.tileSize + 1, mapInfo.tileSize + 1);
            }
        }
    }

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(radarState.terrainCanvas, 0, 0);

    $.each(radarState.players, function (_, player) {
        if (!player?.location) return;
        const x = (player.location.x - radarState.centerX) * scale + canvas.width / 2;
        const y = -(player.location.z - radarState.centerZ) * scale + canvas.height / 2;
        if (x >= -30 && x <= canvas.width + 30 && y >= -30 && y <= canvas.height + 30) {
            drawPlayer(ctx, x, y, player.name);
        }
    });

}

function pollPlayers() {
    $.getJSON(radarState.radarUrl, function (players) {
        radarState.players = Array.isArray(players) ? players : [];
        updatePlayerCards();
        queueRender();
    });
}

function initSevenDaysLiveRadar(radarUrl, mapUrl) {
    if (radarState?.intervalId) clearInterval(radarState.intervalId);
    if (radarState?.animationFrame) cancelAnimationFrame(radarState.animationFrame);
    if (radarState?.renderTimer) clearTimeout(radarState.renderTimer);
    if (radarState?.resizeObserver) radarState.resizeObserver.disconnect();
    if (radarState?.tiles) {
        for (const tile of radarState.tiles.values()) clearTimeout(tile.retryTimer);
    }

    $.getJSON(mapUrl, function (mapInfo) {
        const canvas = $('#map_canvas')[0];
        if (!canvas) return;
        const mapSize = mapInfo.mapSize || { x: 6144, z: 6144 };
        const displaySize = Math.max(320, Math.round($('#map_canvas').width()));
        const fitScale = Math.min(displaySize / mapSize.x, displaySize / mapSize.z);
        const fitZoom = Math.floor(mapInfo.maxZoom + Math.log2(fitScale));

        const terrainCanvas = document.createElement('canvas');
        radarState = {
            canvas,
            ctx: canvas.getContext('2d'),
            terrainCanvas,
            terrainCtx: terrainCanvas.getContext('2d'),
            mapInfo: { ...mapInfo, mapSize },
            radarUrl,
            tileUrl: mapUrl.replace(/\/Map$/i, '/Tile/{z}/{x}/{y}.png'),
            players: [],
            tiles: new Map(),
            zoom: Math.max(0, Math.min(mapInfo.maxZoom, fitZoom)),
            centerX: 0,
            centerZ: 0,
            terrainDirty: true,
            animationFrame: 0,
            renderTimer: 0
        };

        $('#map_name').text(mapInfo.alias || mapInfo.name || '7 Days to Die');
        $('#map_list').css('background-image', 'none');
        $('#map_controls').removeClass('hidden');
        $('.player-team-left-title').text('Survivors');
        $('.player-team-right-title').text('Map');

        let dragging = false;
        let lastX = 0;
        let lastY = 0;
        canvas.onpointerdown = function (event) {
            dragging = true;
            lastX = event.clientX;
            lastY = event.clientY;
            canvas.setPointerCapture(event.pointerId);
        };
        canvas.onpointermove = function (event) {
            if (!dragging) return;
            const scale = scaleAtZoom();
            radarState.centerX -= (event.clientX - lastX) / scale;
            radarState.centerZ += (event.clientY - lastY) / scale;
            lastX = event.clientX;
            lastY = event.clientY;
            clampCenter();
            queueRender(true);
        };
        canvas.onpointerup = canvas.onpointercancel = () => { dragging = false; };
        canvas.onwheel = function (event) {
            event.preventDefault();
            const nextZoom = Math.max(0, Math.min(mapInfo.maxZoom, radarState.zoom + (event.deltaY < 0 ? 1 : -1)));
            if (nextZoom === radarState.zoom) return;
            const bounds = canvas.getBoundingClientRect();
            const mouseX = event.clientX - bounds.left;
            const mouseY = event.clientY - bounds.top;
            const oldScale = scaleAtZoom();
            const worldX = radarState.centerX + (mouseX - canvas.width / 2) / oldScale;
            const worldZ = radarState.centerZ - (mouseY - canvas.height / 2) / oldScale;
            radarState.zoom = nextZoom;
            const newScale = scaleAtZoom();
            radarState.centerX = worldX - (mouseX - canvas.width / 2) / newScale;
            radarState.centerZ = worldZ + (mouseY - canvas.height / 2) / newScale;
            clampCenter();
            queueRender(true, 80);
        };

        radarState.resizeObserver = new ResizeObserver(() => {
            if (resizeCanvas()) queueRender(true);
        });
        radarState.resizeObserver.observe(canvas);
        pollPlayers();
        radarState.intervalId = setInterval(pollPlayers, 1000);
        queueRender(true);
    });
}

window.initSevenDaysLiveRadar = initSevenDaysLiveRadar;
})();

window.initLiveRadar = function (radarDataUrl, mapDataUrl) {
    $.getJSON(mapDataUrl, function (mapInfo) {
        if (mapInfo && mapInfo.provider === 'd7d') {
            window.initSevenDaysLiveRadar(radarDataUrl, mapDataUrl);
            return;
        }
        initCallOfDutyLiveRadar(radarDataUrl, mapDataUrl);
    }).fail(function (jqxhr, textStatus, error) {
        console.error('[LiveRadar] Map metadata fetch failed:', textStatus, error);
    });
};

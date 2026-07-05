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

    return `/_content/liveradar/images/hud_weapons/hud_${weapons[name]}.png`;
}

function updatePlayerData() {
    $('.player-data-left').html('');
    $('.player-data-right').html('');

    $.each(newRadarData, function (index, player) {
        if (player == null) {
            return;
        }

        const column = player.team === 'allies' ? $('.player-data-left') : $('.player-data-right');
        const accent = player.team === 'allies' ? 'rgb(0,122,204)' : 'rgb(255,69,69)';
        const dead = player.health <= 0;
        const health = Math.max(0, Math.min(100, player.health));
        const kd = player.deaths === 0 ? player.kills.toFixed(2) : (player.kills / player.deaths).toFixed(2);
        const spm = player.playTime === 0 ? '&mdash;' : Math.round(player.score / (player.playTime / 60));

        column.append(`
<div class="rounded-lg border border-line/60 bg-surface-alt/30 overflow-hidden transition-colors hover:border-line ${dead ? 'opacity-60' : ''}">
    <div class="relative h-7 w-full bg-black/30">
        <div class="absolute inset-y-0 left-0 transition-all duration-300" style="width:${health}%; background:${accent}"></div>
        <div class="absolute inset-0 flex items-center justify-between gap-2 px-2.5 z-10">
            <span class="text-xs font-bold text-white drop-shadow truncate">${player.name}</span>
            <span class="text-[10px] font-mono shrink-0 ${dead ? 'text-rose-300' : 'text-white/80'}">${dead ? 'DEAD' : health}</span>
        </div>
    </div>
    <div class="px-2.5 py-1.5 flex items-center justify-between gap-2 text-xs text-foreground/90">
        <div class="w-11 h-5 bg-contain bg-no-repeat bg-left shrink-0 ${dead ? 'opacity-40' : 'opacity-90'}" style="background-image:url(${weaponImageForWeapon(player.weapon)})" title="${player.weapon}"></div>
        <div class="flex items-center gap-2.5 font-mono">
            <span class="flex items-center gap-1" title="Kills"><i class="ph ph-skull text-muted"></i>${player.kills}</span>
            <span class="flex items-center gap-1" title="Deaths"><i class="ph ph-skull text-muted opacity-50"></i>${player.deaths}</span>
            <span class="flex items-center gap-1" title="K/D Ratio"><i class="ph ph-divide text-muted"></i>${kd}</span>
            <span class="flex items-center gap-1" title="Score/Min"><i class="ph ph-chart-line-up text-muted"></i>${spm}</span>
        </div>
    </div>
</div>`);
    });
}

// Map metadata, pushed from the server over the Blazor circuit (was a JSON fetch).
window.setMapData = function (_map) {
    if (!stateInfo || _map == null) {
        return;
    }

    stateInfo.mapInfo = _map;
    $('#map_name').html(_map.alias);
    $('#map_list').css('background-image', `url(/_content/liveradar/images/minimaps/compass_map_${_map.name}@2x.jpg)`);
    checkCanvasSize(stateInfo.canvas, stateInfo.ctx, $('#map_list'), _map);
};

// Player snapshot, pushed from the server over the Blazor circuit on the radar update cadence (was a polled
// JSON fetch). Runs the same previous-vs-current snapshot diff the poll used to.
window.setRadarData = function (_radarItem) {
    if (!stateInfo || !stateInfo.mapInfo) {
        return;
    }

    newRadarData = _radarItem;

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

    checkCanvasSize(stateInfo.canvas, stateInfo.ctx, $('#map_list'), stateInfo.mapInfo);
    updatePlayerData();
};

function updateMap() {
    // the render loop starts immediately; idle until the first map snapshot has been pushed from the server
    if (!stateInfo || !stateInfo.mapInfo) {
        window.requestAnimationFrame(updateMap);
        return;
    }

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

window.initLiveRadar = function () {
    if ($('#map_canvas').length === 0) {
        console.error("[LiveRadar] Canvas #map_canvas not found!");
        return;
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

    // Reset snapshot history so a server/page switch doesn't interpolate across maps.
    previousRadarData = undefined;
    newRadarData = undefined;

    // Map metadata and player snapshots are pushed from the server (setMapData / setRadarData) over the
    // Blazor circuit — no HTTP polling. Just run the render loop; it idles until the first map arrives.
    window.requestAnimationFrame(updateMap);
}

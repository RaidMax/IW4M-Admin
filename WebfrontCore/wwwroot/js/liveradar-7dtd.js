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

export function initSevenDaysLiveRadar(radarUrl, mapUrl) {
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

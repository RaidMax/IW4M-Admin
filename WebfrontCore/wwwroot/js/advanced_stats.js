window.onresize = function () {
    if (window.hitLocationData) {
        drawPlayerModel();
    }
}

window.initAdvancedStats = function (history, hitLocations, maxPct, performanceText) {
    // Store globally for resize event
    window.hitLocationData = hitLocations;
    window.maxPercentage = maxPct;
    window.performanceHistory = history;

    // Setup Performance
    const chart = document.getElementById('client_performance_history');
    if (chart) {
        renderPerformanceChart(performanceText);
    }

    // Setup Hitmodel
    drawPlayerModel();
}

function drawPlayerModel() {
    const canvas = document.getElementById('hitlocation_model');
    if (canvas === null) {
        return;
    }
    const context = canvas.getContext('2d');
    const container = document.getElementById('hitlocation_container');
    if (!container) {
        return;
    }
    const background = new Image();
    background.onload = () => {
        const backgroundRatioX = background.width / background.height;

        canvas.height = container.clientHeight - 28;
        canvas.width = (canvas.height * backgroundRatioX);

        const scalar = canvas.height / background.height;

        drawHitLocationChart(context, background, scalar, canvas.width, canvas.height);
    }
    background.src = '/images/stats/hit_location_model.jpg';
}

function buildHitLocationPosition() {
    // Ultra-precise multi-point polygon coordinates for hit_location_model.jpg (300x441)
    // Refined for exact silhouette alignment, zero gaps at joints, and smooth curves
    let hitLocations = {};

    // Head - refined helmet/head shape
    hitLocations['head'] = {
        points: [
            { x: 150, y: 22 }, { x: 138, y: 25 }, { x: 128, y: 35 }, { x: 124, y: 50 },
            { x: 124, y: 65 }, { x: 130, y: 78 }, { x: 140, y: 84 }, { x: 150, y: 86 },
            { x: 160, y: 84 }, { x: 170, y: 78 }, { x: 176, y: 65 }, { x: 176, y: 50 },
            { x: 172, y: 35 }, { x: 162, y: 25 }
        ],
        type: 'polygon'
    };

    // Torso upper - shoulders, chest, joints with arms
    hitLocations['torso_upper'] = {
        points: [
            { x: 140, y: 84 }, { x: 125, y: 75 }, { x: 105, y: 85 }, { x: 90, y: 115 },
            { x: 105, y: 145 }, { x: 115, y: 175 }, { x: 185, y: 175 }, { x: 195, y: 145 },
            { x: 210, y: 115 }, { x: 195, y: 85 }, { x: 175, y: 75 }, { x: 160, y: 84 }
        ],
        type: 'polygon'
    };

    // Torso lower - belly, hips, utility belt
    hitLocations['torso_lower'] = {
        points: [
            { x: 115, y: 175 }, { x: 95, y: 200 }, { x: 98, y: 228 }, { x: 150, y: 235 },
            { x: 202, y: 228 }, { x: 205, y: 200 }, { x: 185, y: 175 }
        ],
        type: 'polygon'
    };

    // Right arm upper (viewer's left)
    hitLocations['right_arm_upper'] = {
        points: [
            { x: 90, y: 115 }, { x: 105, y: 145 }, { x: 78, y: 165 }, { x: 45, y: 142 }, { x: 65, y: 105 }
        ],
        type: 'polygon'
    };

    // Left arm upper (viewer's right)
    hitLocations['left_arm_upper'] = {
        points: [
            { x: 210, y: 115 }, { x: 195, y: 145 }, { x: 222, y: 165 }, { x: 255, y: 142 }, { x: 235, y: 105 }
        ],
        type: 'polygon'
    };

    // Right arm lower (viewer's left)
    hitLocations['right_arm_lower'] = {
        points: [
            { x: 45, y: 142 }, { x: 78, y: 165 }, { x: 45, y: 192 }, { x: 15, y: 168 }
        ],
        type: 'polygon'
    };

    // Left arm lower (viewer's right)
    hitLocations['left_arm_lower'] = {
        points: [
            { x: 255, y: 142 }, { x: 222, y: 165 }, { x: 255, y: 192 }, { x: 285, y: 168 }
        ],
        type: 'polygon'
    };

    // Right hand (viewer's left)
    hitLocations['right_hand'] = {
        points: [
            { x: 15, y: 168 }, { x: 45, y: 192 }, { x: 40, y: 210 }, { x: 18, y: 215 }, { x: 0, y: 200 }, { x: 5, y: 170 }
        ],
        type: 'polygon'
    };

    // Left hand (viewer's right)
    hitLocations['left_hand'] = {
        points: [
            { x: 285, y: 168 }, { x: 255, y: 192 }, { x: 260, y: 210 }, { x: 282, y: 215 }, { x: 300, y: 200 }, { x: 295, y: 170 }
        ],
        type: 'polygon'
    };

    // Right leg upper (viewer's left) - thigh
    hitLocations['right_leg_upper'] = {
        points: [
            { x: 98, y: 228 }, { x: 150, y: 235 }, { x: 150, y: 310 }, { x: 95, y: 310 }, { x: 98, y: 260 }
        ],
        type: 'polygon'
    };

    // Left leg upper (viewer's right) - thigh
    hitLocations['left_leg_upper'] = {
        points: [
            { x: 202, y: 228 }, { x: 150, y: 235 }, { x: 150, y: 310 }, { x: 205, y: 310 }, { x: 202, y: 260 }
        ],
        type: 'polygon'
    };

    // Right leg lower (viewer's left) - shin
    hitLocations['right_leg_lower'] = {
        points: [
            { x: 95, y: 310 }, { x: 150, y: 310 }, { x: 150, y: 400 }, { x: 105, y: 400 }, { x: 100, y: 360 }
        ],
        type: 'polygon'
    };

    // Left leg lower (viewer's right) - shin
    hitLocations['left_leg_lower'] = {
        points: [
            { x: 205, y: 310 }, { x: 150, y: 310 }, { x: 150, y: 400 }, { x: 195, y: 400 }, { x: 200, y: 360 }
        ],
        type: 'polygon'
    };

    // Right foot (viewer's left)
    hitLocations['right_foot'] = {
        points: [
            { x: 105, y: 400 }, { x: 150, y: 400 }, { x: 150, y: 438 }, { x: 95, y: 438 }, { x: 92, y: 425 }
        ],
        type: 'polygon'
    };

    // Left foot (viewer's right)
    hitLocations['left_foot'] = {
        points: [
            { x: 195, y: 400 }, { x: 150, y: 400 }, { x: 150, y: 438 }, { x: 205, y: 438 }, { x: 208, y: 425 }
        ],
        type: 'polygon'
    };

    return hitLocations;
}

function drawHitLocationChart(context, background, scalar, width, height) {
    context.drawImage(background, 0, 0, background.width, background.height, 0, 0, width, height);

    const hitLocations = buildHitLocationPosition();

    window.hitLocationData.forEach((hit) => {
        let scaledPercentage = hit.percentage / window.maxPercentage;
        let red;
        let green = 255;

        if (scaledPercentage < 0.5) {
            red = Math.round(scaledPercentage * 255 * 2);
        } else {
            red = 255;
            green = Math.round((1 - scaledPercentage) * 255 * 2);
        }

        red = red.toString(16).padStart(2, '0');
        green = green.toString(16).padStart(2, '0');

        const color = '#' + red + green + '0077';
        const location = hitLocations[hit.name];

        if (location === undefined) {
            return;
        }

        // All locations are now polygons with variable point counts
        drawPolygon(context, scalar, location.points, color);
    });
}

function drawPolygon(context, scalar, points, color) {
    if (!points || points.length < 3) return;

    // Scale each point
    const scaledPoints = points.map(p => ({
        x: p.x * scalar,
        y: p.y * scalar
    }));

    context.beginPath();
    context.fillStyle = color;
    context.moveTo(scaledPoints[0].x, scaledPoints[0].y);

    for (let i = 1; i < scaledPoints.length; i++) {
        context.lineTo(scaledPoints[i].x, scaledPoints[i].y);
    }

    context.closePath();
    context.fill();
}

function renderPerformanceChart(performanceText) {
    const id = 'client_performance_history';
    const data = window.performanceHistory;

    if (data === undefined || data === null) {
        return;
    }

    getStatsChart(id, performanceText, data);
}

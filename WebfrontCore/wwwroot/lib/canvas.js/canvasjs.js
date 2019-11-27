/**
 * @preserve CanvasJS HTML5 & JavaScript Charts - v1.7.0 GA - http://canvasjs.com/ 
 * Copyright 2013 fenopix
 */
(function() {
	function w(n, t) {
		n.prototype = yi(t.prototype);
		n.prototype.constructor = n;
		n.base = t.prototype
	}

	function yi(n) {
		function t() {}
		return t.prototype = n, new t
	}

	function oi(n, t, i) {
		return i === "millisecond" ? n.setMilliseconds(n.getMilliseconds() + 1 * t) : i === "second" ? n.setSeconds(n.getSeconds() + 1 * t) : i === "minute" ? n.setMinutes(n.getMinutes() + 1 * t) : i === "hour" ? n.setHours(n.getHours() + 1 * t) : i === "day" ? n.setDate(n.getDate() + 1 * t) : i === "week" ? n.setDate(n.getDate() + 7 * t) : i === "month" ? n.setMonth(n.getMonth() + 1 * t) : i === "year" && n.setFullYear(n.getFullYear() + 1 * t), n
	}

	function st(n, t) {
		return f[t + "Duration"] * n
	}

	function v(n, t) {
		var i = !1;
		for (n < 0 && (i = !0, n *= -1), n = "" + n, t = t ? t : 1; n.length < t;) n = "0" + n;
		return i ? "-" + n : n
	}

	function ht(n) {
		if (!n) return n;
		n = n.replace(/^\s\s*/, "");
		for (var t = n.length;
			/\s/.test(n.charAt(--t)););
		return n.slice(0, t + 1)
	}

	function pi(n) {
		n.roundRect = function(n, t, i, r, u, f, e, o) {
			e && (this.fillStyle = e);
			o && (this.strokeStyle = o);
			typeof u == "undefined" && (u = 5);
			this.lineWidth = f;
			this.beginPath();
			this.moveTo(n + u, t);
			this.lineTo(n + i - u, t);
			this.quadraticCurveTo(n + i, t, n + i, t + u);
			this.lineTo(n + i, t + r - u);
			this.quadraticCurveTo(n + i, t + r, n + i - u, t + r);
			this.lineTo(n + u, t + r);
			this.quadraticCurveTo(n, t + r, n, t + r - u);
			this.lineTo(n, t + u);
			this.quadraticCurveTo(n, t, n + u, t);
			this.closePath();
			e && this.fill();
			o && f > 0 && this.stroke()
		}
	}

	function si(n, t) {
		return n - t
	}

	function wi(n, t) {
		return n.x - t.x
	}

	function u(n) {
		var t = ((n & 16711680) >> 16).toString(16),
			i = ((n & 65280) >> 8).toString(16),
			r = ((n & 255) >> 0).toString(16);
		return t = t.length < 2 ? "0" + t : t, i = i.length < 2 ? "0" + i : i, r = r.length < 2 ? "0" + r : r, "#" + t + i + r
	}

	function bi(n, t, i) {
		return n << 16 | t << 8 | i
	}

	function ki(n) {
		var i = this.length >>> 0,
			t = Number(arguments[1]) || 0;
		for (t = t < 0 ? Math.ceil(t) : Math.floor(t), t < 0 && (t += i); t < i; t++)
			if (t in this && this[t] === n) return t;
		return -1
	}

	function di(n) {
		return n.indexOf || (n.indexOf = ki), n
	}

	function pt(n, t, i) {
		var u, r, f, e, o;
		if (i = i || "normal", u = n + "_" + t + "_" + i, r = hi[u], isNaN(r)) {
			try {
				f = "position:absolute; left:0px; top:-20000px; padding:0px;margin:0px;border:none;white-space:pre;line-height:normal;font-family:" + n + "; font-size:" + t + "px; font-weight:" + i + ";";
				g || (e = document.body, g = document.createElement("span"), g.innerHTML = "", o = document.createTextNode("Mpgyi"), g.appendChild(o), e.appendChild(g));
				g.style.display = "";
				g.setAttribute("style", f);
				r = Math.round(g.offsetHeight);
				g.style.display = "none"
			} catch (s) {
				r = Math.ceil(t * 1.1)
			}
			r = Math.max(r, t);
			hi[u] = r
		}
		return r
	}

	function y(n, t) {
		var i = [],
			r;
		if (n = n || "solid", lineDashTypeMap = {
				solid: [],
				shortDash: [3, 1],
				shortDot: [1, 1],
				shortDashDot: [3, 1, 1, 1],
				shortDashDotDot: [3, 1, 1, 1, 1, 1],
				dot: [1, 2],
				dash: [4, 2],
				dashDot: [4, 2, 1, 2],
				longDash: [8, 2],
				longDashDot: [8, 2, 1, 2],
				longDashDotDot: [8, 2, 1, 2, 1, 2]
			}, i = lineDashTypeMap[n], i)
			for (r = 0; r < i.length; r++) i[r] *= t;
		else i = [];
		return i
	}

	function s(n, t, i, r) {
		if (n.addEventListener) n.addEventListener(t, i, r || !1);
		else if (n.attachEvent) n.attachEvent("on" + t, function(t) {
			t = t || window.event;
			t.preventDefault = t.preventDefault || function() {
				t.returnValue = !1
			};
			t.stopPropagation = t.stopPropagation || function() {
				t.cancelBubble = !0
			};
			i.call(n, t)
		});
		else return !1
	}

	function ci(n, t, i) {
		var r, f, u;
		for (n *= l, t *= l, r = i.getImageData(n, t, 2, 2).data, f = !0, u = 0; u < 4; u++)
			if (r[u] !== r[u + 4] | r[u] !== r[u + 8] | r[u] !== r[u + 12]) {
				f = !1;
				break
			} return f ? bi(r[0], r[1], r[2]) : 0
	}

	function gi(t, i, r) {
		var u = "",
			e = t ? t + "FontStyle" : "fontStyle",
			o = t ? t + "FontWeight" : "fontWeight",
			s = t ? t + "FontSize" : "fontSize",
			h = t ? t + "FontFamily" : "fontFamily",
			c, f;
		return u += i[e] ? i[e] + " " : r && r[e] ? r[e] + " " : "", u += i[o] ? i[o] + " " : r && r[o] ? r[o] + " " : "", u += i[s] ? i[s] + "px " : r && r[s] ? r[s] + "px " : "", c = i[h] ? i[h] + "" : r && r[h] ? r[h] + "" : "", !n && c ? (f = c.split(",")[0], f[0] !== "'" && f[0] !== '"' && (f = "'" + f + "'"), u += f) : u += c, u
	}

	function p(n, t, i) {
		return n in t ? t[n] : i[n]
	}

	function ct(t, i, r) {
		if (n && !!li) {
			var u = t.getContext("2d");
			bt = u.webkitBackingStorePixelRatio || u.mozBackingStorePixelRatio || u.msBackingStorePixelRatio || u.oBackingStorePixelRatio || u.backingStorePixelRatio || 1;
			l = ri / bt;
			t.width = i * l;
			t.height = r * l;
			ri !== bt && (t.style.width = i + "px", t.style.height = r + "px", u.scale(l, l))
		} else t.width = i, t.height = r
	}

	function rt(t, i) {
		var r = document.createElement("canvas");
		return r.setAttribute("class", "canvasjs-chart-canvas"), ct(r, t, i), n || typeof G_vmlCanvasManager == "undefined" || G_vmlCanvasManager.initElement(r), r
	}

	function ai(n, t, i) {
		var u, o, s;
		if (n && t && i) {
			var h = i + "." + (t === "jpeg" ? "jpg" : t),
				c = "image/" + t,
				f = n.toDataURL(c),
				l = !1,
				r = document.createElement("a");
			if (r.download = h, r.href = f, r.target = "_blank", typeof Blob != "undefined" && !!new Blob) {
				var v = f.replace(/^data:[a-z/]*;base64,/, ""),
					e = atob(v),
					a = new ArrayBuffer(e.length),
					y = new Uint8Array(a);
				for (u = 0; u < e.length; u++) y[u] = e.charCodeAt(u);
				o = new Blob([a], {
					type: "image/" + t
				});
				try {
					window.navigator.msSaveBlob(o, h);
					l = !0
				} catch (p) {
					r.dataset.downloadurl = [c, r.download, r.href].join(":");
					r.href = window.URL.createObjectURL(o)
				}
			}
			if (!l) try {
				event = document.createEvent("MouseEvents");
				event.initMouseEvent("click", !0, !1, window, 0, 0, 0, 0, 0, !1, !1, !1, !1, 0, null);
				r.dispatchEvent ? r.dispatchEvent(event) : r.fireEvent && r.fireEvent("onclick")
			} catch (p) {
				s = window.open();
				s.document.write("<img src='" + f + "'><\/img><div>Please right click on the image and save it to your device<\/div>");
				s.document.close()
			}
		}
	}

	function b(n, t, i) {
		t.getAttribute("state") !== i && (t.setAttribute("state", i), t.setAttribute("type", "button"), t.style.position = "relative", t.style.margin = "0px 0px 0px 0px", t.style.padding = "3px 4px 0px 4px", t.style.cssFloat = "left", t.setAttribute("title", n._cultureInfo[i + "Text"]), t.innerHTML = "<img style='height:16px;' src='" + nr[i].image + "' alt='" + n._cultureInfo[i + "Text"] + "' />")
	}

	function ui() {
		for (var n = null, t = 0; t < arguments.length; t++) n = arguments[t], n.style && (n.style.display = "inline")
	}

	function nt() {
		for (var n = null, t = 0; t < arguments.length; t++) n = arguments[t], n && n.style && (n.style.display = "none")
	}

	function h(n, t, i, r) {
		this._defaultsKey = n;
		this.parent = r;
		this._eventListeners = [];
		var u = {};
		i && ut[i] && ut[i][n] && (u = ut[i][n]);
		this._options = t ? t : {};
		this.setOptions(this._options, u)
	}

	function t(i, r, u) {
		var f, e, o;
		if (this._publicChartReference = u, r = r || {}, t.base.constructor.call(this, "Chart", r, r.theme ? r.theme : "theme1"), f = this, this._containerId = i, this._objectsInitialized = !1, this.ctx = null, this.overlaidCanvasCtx = null, this._indexLabels = [], this._panTimerId = 0, this._lastTouchEventType = "", this._lastTouchData = null, this.isAnimating = !1, this.renderCount = 0, this.animatedRender = !1, this.disableToolTip = !1, this.panEnabled = !1, this._defaultCursor = "default", this.plotArea = {
				canvas: null,
				ctx: null,
				x1: 0,
				y1: 0,
				x2: 0,
				y2: 0,
				width: 0,
				height: 0
			}, this._dataInRenderedOrder = [], this._container = typeof this._containerId == "string" ? document.getElementById(this._containerId) : this._containerId, !this._container) {
			window.console && window.console.log('CanvasJS Error: Chart Container with id "' + this._containerId + '" was not found');
			return
		}
		if (this._container.innerHTML = "", e = 0, o = 0, e = this._options.width ? this.width : this._container.clientWidth > 0 ? this._container.clientWidth : this.width, o = this._options.height ? this.height : this._container.clientHeight > 0 ? this._container.clientHeight : this.height, this.width = e, this.height = o, this.x1 = this.y1 = 0, this.x2 = this.width, this.y2 = this.height, this._selectedColorSet = typeof tt[this.colorSet] != "undefined" ? tt[this.colorSet] : tt.colorSet1, this._canvasJSContainer = document.createElement("div"), this._canvasJSContainer.setAttribute("class", "canvasjs-chart-container"), this._canvasJSContainer.style.position = "relative", this._canvasJSContainer.style.textAlign = "left", this._canvasJSContainer.style.cursor = "auto", n || (this._canvasJSContainer.style.height = "0px"), this._container.appendChild(this._canvasJSContainer), this.canvas = rt(e, o), this.canvas.style.position = "absolute", this.canvas.getContext) this._canvasJSContainer.appendChild(this.canvas), this.ctx = this.canvas.getContext("2d"), this.ctx.textBaseline = "top", pi(this.ctx);
		else return;
		n ? this.plotArea.ctx = this.ctx : (this.plotArea.canvas = rt(e, o), this.plotArea.canvas.style.position = "absolute", this.plotArea.canvas.setAttribute("class", "plotAreaCanvas"), this._canvasJSContainer.appendChild(this.plotArea.canvas), this.plotArea.ctx = this.plotArea.canvas.getContext("2d"));
		this.overlaidCanvas = rt(e, o);
		this.overlaidCanvas.style.position = "absolute";
		this._canvasJSContainer.appendChild(this.overlaidCanvas);
		this.overlaidCanvasCtx = this.overlaidCanvas.getContext("2d");
		this.overlaidCanvasCtx.textBaseline = "top";
		this._eventManager = new at(this);
		s(window, "resize", function() {
			f._updateSize() && f.render()
		});
		this._toolBar = document.createElement("div");
		this._toolBar.setAttribute("class", "canvasjs-chart-toolbar");
		this._toolBar.style.cssText = "position: absolute; right: 1px; top: 1px;";
		this._canvasJSContainer.appendChild(this._toolBar);
		this.bounds = {
			x1: 0,
			y1: 0,
			x2: this.width,
			y2: this.height
		};
		s(this.overlaidCanvas, "click", function(n) {
			f._mouseEventHandler(n)
		});
		s(this.overlaidCanvas, "mousemove", function(n) {
			f._mouseEventHandler(n)
		});
		s(this.overlaidCanvas, "mouseup", function(n) {
			f._mouseEventHandler(n)
		});
		s(this.overlaidCanvas, "mousedown", function(n) {
			f._mouseEventHandler(n);
			nt(f._dropdownMenu)
		});
		s(this.overlaidCanvas, "mouseout", function(n) {
			f._mouseEventHandler(n)
		});
		s(this.overlaidCanvas, window.navigator.msPointerEnabled ? "MSPointerDown" : "touchstart", function(n) {
			f._touchEventHandler(n)
		});
		s(this.overlaidCanvas, window.navigator.msPointerEnabled ? "MSPointerMove" : "touchmove", function(n) {
			f._touchEventHandler(n)
		});
		s(this.overlaidCanvas, window.navigator.msPointerEnabled ? "MSPointerUp" : "touchend", function(n) {
			f._touchEventHandler(n)
		});
		s(this.overlaidCanvas, window.navigator.msPointerEnabled ? "MSPointerCancel" : "touchcancel", function(n) {
			f._touchEventHandler(n)
		});
		this._creditLink || (this._creditLink = document.createElement("a"), this._creditLink.setAttribute("class", "canvasjs-chart-credit"), this._creditLink.setAttribute("style", "outline:none;margin:0px;position:absolute;right:3px;top:" + (this.height - 14) + "px;color:dimgrey;text-decoration:none;font-size:10px;font-family:Lucida Grande, Lucida Sans Unicode, Arial, sans-serif"), this._creditLink.setAttribute("tabIndex", -1), this._creditLink.setAttribute("target", "_blank"));
		this._toolTip = new k(this, this._options.toolTip, this.theme);
		this.data = null;
		this.axisX = null;
		this.axisY = null;
		this.axisY2 = null;
		this.sessionVariables = {
			axisX: {
				internalMinimum: null,
				internalMaximum: null
			},
			axisY: {
				internalMinimum: null,
				internalMaximum: null
			},
			axisY2: {
				internalMinimum: null,
				internalMaximum: null
			}
		}
	}

	function kt(n, t) {
		for (var f, e, i, o, h, s, c, r = [], u = 0; u < n.length; u++) {
			if (u == 0) {
				r.push(n[0]);
				continue
			}
			i = u - 1;
			f = i === 0 ? 0 : i - 1;
			e = i === n.length - 1 ? i : i + 1;
			o = {
				x: (n[e].x - n[f].x) / t,
				y: (n[e].y - n[f].y) / t
			};
			h = {
				x: n[i].x + o.x / 3,
				y: n[i].y + o.y / 3
			};
			r[r.length] = h;
			i = u;
			f = i === 0 ? 0 : i - 1;
			e = i === n.length - 1 ? i : i + 1;
			s = {
				x: (n[e].x - n[f].x) / t,
				y: (n[e].y - n[f].y) / t
			};
			c = {
				x: n[i].x - s.x / 3,
				y: n[i].y - s.y / 3
			};
			r[r.length] = c;
			r[r.length] = n[u]
		}
		return r
	}

	function ft(n, t, i, r, u) {
		typeof u == "undefined" && (u = 0);
		this._padding = u;
		this._x1 = n;
		this._y1 = t;
		this._x2 = i;
		this._y2 = r;
		this._topOccupied = this._padding;
		this._bottomOccupied = this._padding;
		this._leftOccupied = this._padding;
		this._rightOccupied = this._padding
	}

	function c(n, t) {
		c.base.constructor.call(this, "TextBlock", t);
		this.ctx = n;
		this._isDirty = !0;
		this._wrappedText = null;
		this._lineHeight = pt(this.fontFamily, this.fontSize, this.fontWeight)
	}

	function lt(n, t) {
		lt.base.constructor.call(this, "Title", t, n.theme);
		this.chart = n;
		this.canvas = n.canvas;
		this.ctx = this.chart.ctx;
		typeof this._options.fontSize == "undefined" && (this.fontSize = this.chart.getAutoFontSize(this.fontSize));
		this.width = null;
		this.height = null;
		this.bounds = {
			x1: null,
			y1: null,
			x2: null,
			y2: null
		}
	}

	function gt(n, t) {
		gt.base.constructor.call(this, "Subtitle", t, n.theme);
		this.chart = n;
		this.canvas = n.canvas;
		this.ctx = this.chart.ctx;
		typeof this._options.fontSize == "undefined" && (this.fontSize = this.chart.getAutoFontSize(this.fontSize));
		this.width = null;
		this.height = null;
		this.bounds = {
			x1: null,
			y1: null,
			x2: null,
			y2: null
		}
	}

	function ni(n, t, i) {
		ni.base.constructor.call(this, "Legend", t, i);
		this.chart = n;
		this.canvas = n.canvas;
		this.ctx = this.chart.ctx;
		this.ghostCtx = this.chart._eventManager.ghostCtx;
		this.items = [];
		this.width = 0;
		this.height = 0;
		this.orientation = null;
		this.dataSeries = [];
		this.bounds = {
			x1: null,
			y1: null,
			x2: null,
			y2: null
		};
		typeof this._options.fontSize == "undefined" && (this.fontSize = this.chart.getAutoFontSize(this.fontSize));
		this.lineHeight = pt(this.fontFamily, this.fontSize, this.fontWeight);
		this.horizontalSpacing = this.fontSize
	}

	function fi(n, t) {
		fi.base.constructor.call(this, t);
		this.chart = n;
		this.canvas = n.canvas;
		this.ctx = this.chart.ctx
	}

	function d(n, t, i, r, u) {
		d.base.constructor.call(this, "DataSeries", t, i);
		this.chart = n;
		this.canvas = n.canvas;
		this._ctx = n.canvas.ctx;
		this.index = r;
		this.noDataPointsInPlotArea = 0;
		this.id = u;
		this.chart._eventManager.objectMap[u] = {
			id: u,
			objectType: "dataSeries",
			dataSeriesIndex: r
		};
		this.dataPointIds = [];
		this.plotUnit = [];
		this.axisX = null;
		this.axisY = null;
		this.fillOpacity === null && (this.fillOpacity = this.type.match(/area/i) ? .7 : 1);
		this.axisPlacement = this.getDefaultAxisPlacement();
		typeof this._options.indexLabelFontSize == "undefined" && (this.indexLabelFontSize = this.chart.getAutoFontSize(this.indexLabelFontSize))
	}

	function e(n, t, i, r) {
		if (e.base.constructor.call(this, "Axis", t, n.theme), this.chart = n, this.canvas = n.canvas, this.ctx = n.ctx, this.maxWidth = 0, this.maxHeight = 0, this.intervalstartTimePercent = 0, this.labels = [], this._labels = null, this.dataInfo = {
				min: Infinity,
				max: -Infinity,
				viewPortMin: Infinity,
				viewPortMax: -Infinity,
				minDiff: Infinity
			}, i === "axisX" ? (this.sessionVariables = this.chart.sessionVariables[i], this._options.interval || (this.intervalType = null)) : this.sessionVariables = r === "left" || r === "top" ? this.chart.sessionVariables.axisY : this.chart.sessionVariables.axisY2, typeof this._options.titleFontSize == "undefined" && (this.titleFontSize = this.chart.getAutoFontSize(this.titleFontSize)), typeof this._options.labelFontSize == "undefined" && (this.labelFontSize = this.chart.getAutoFontSize(this.labelFontSize)), this.type = i, i !== "axisX" || t && typeof t.gridThickness != "undefined" || (this.gridThickness = 0), this._position = r, this.lineCoordinates = {
				x1: null,
				y1: null,
				x2: null,
				y2: null,
				width: null
			}, this.labelAngle = (this.labelAngle % 360 + 360) % 360, this.labelAngle > 90 && this.labelAngle <= 270 ? this.labelAngle -= 180 : this.labelAngle > 180 && this.labelAngle <= 270 ? this.labelAngle -= 180 : this.labelAngle > 270 && this.labelAngle <= 360 && (this.labelAngle -= 360), this._options.stripLines && this._options.stripLines.length > 0) {
			this.stripLines = [];
			for (var u = 0; u < this._options.stripLines.length; u++) this.stripLines.push(new ti(this.chart, this._options.stripLines[u], n.theme, ++this.chart._eventManager.lastObjectId, this))
		}
		this._titleTextBlock = null;
		this._absoluteMinimum = null;
		this._absoluteMaximum = null;
		this.hasOptionChanged("minimum") && (this.sessionVariables.internalMinimum = this.minimum);
		this.hasOptionChanged("maximum") && (this.sessionVariables.internalMaximum = this.maximum);
		this.trackChanges("minimum");
		this.trackChanges("maximum")
	}

	function ti(n, t, i, r, u) {
		ti.base.constructor.call(this, "StripLine", t, i, u);
		this.id = r;
		this.chart = n;
		this.ctx = this.chart.ctx;
		this.label = this.label;
		this._thicknessType = "pixel";
		this.startValue !== null && this.endValue !== null && (this.value = ((this.startValue.getTime ? this.startValue.getTime() : this.startValue) + (this.endValue.getTime ? this.endValue.getTime() : this.endValue)) / 2, this.thickness = Math.max(this.endValue - this.startValue), this._thicknessType = "value")
	}

	function k(n, t, i) {
		k.base.constructor.call(this, "ToolTip", t, i);
		this.chart = n;
		this.canvas = n.canvas;
		this.ctx = this.chart.ctx;
		this.currentSeriesIndex = -1;
		this.currentDataPointIndex = -1;
		this._timerId = 0;
		this._prevX = NaN;
		this._prevY = NaN;
		this._initialize()
	}

	function at(n) {
		var t, i;
		this.chart = n;
		this.lastObjectId = 0;
		t = this;
		this.objectMap = [];
		this.rectangularRegionEventSubscriptions = [];
		this.previousDataPointEventObject = null;
		this.ghostCanvas = rt(this.chart.width, this.chart.height);
		this.ghostCtx = this.ghostCanvas.getContext("2d");
		i = function(n) {
			t.mouseEventHandler.call(t, n)
		};
		this.mouseoveredObjectMaps = []
	}

	function vt(n) {
		var t;
		n && ot[n] && (t = ot[n]);
		vt.base.constructor.call(this, "CultureInfo", t)
	}

	function ei(n) {
		this.chart = n;
		this.ctx = this.chart.plotArea.ctx;
		this.animations = [];
		this.animationRequestId = null
	}
	var yt = !1,
		n = !!document.createElement("canvas").getContext,
		et = {
			Chart: {
				width: 500,
				height: 400,
				zoomEnabled: !1,
				backgroundColor: "white",
				theme: "theme1",
				animationEnabled: !1,
				animationDuration: 1200,
				dataPointMaxWidth: null,
				colorSet: "colorSet1",
				culture: "en",
				creditText: "CanvasJS.com",
				interactivityEnabled: !0,
				exportEnabled: !1,
				exportFileName: "Chart"
			},
			Title: {
				padding: 0,
				text: null,
				verticalAlign: "top",
				horizontalAlign: "center",
				fontSize: 20,
				fontFamily: "Calibri",
				fontWeight: "normal",
				fontColor: "black",
				fontStyle: "normal",
				borderThickness: 0,
				borderColor: "black",
				cornerRadius: 0,
				backgroundColor: null,
				margin: 5,
				wrap: !0,
				maxWidth: null,
				dockInsidePlotArea: !1
			},
			Subtitle: {
				padding: 0,
				text: null,
				verticalAlign: "top",
				horizontalAlign: "center",
				fontSize: 14,
				fontFamily: "Calibri",
				fontWeight: "normal",
				fontColor: "black",
				fontStyle: "normal",
				borderThickness: 0,
				borderColor: "black",
				cornerRadius: 0,
				backgroundColor: null,
				margin: 2,
				wrap: !0,
				maxWidth: null,
				dockInsidePlotArea: !1
			},
			Legend: {
				name: null,
				verticalAlign: "center",
				horizontalAlign: "right",
				fontSize: 14,
				fontFamily: "calibri",
				fontWeight: "normal",
				fontColor: "black",
				fontStyle: "normal",
				cursor: null,
				itemmouseover: null,
				itemmouseout: null,
				itemmousemove: null,
				itemclick: null,
				dockInsidePlotArea: !1,
				reversed: !1,
				maxWidth: null,
				maxHeight: null,
				itemMaxWidth: null,
				itemWidth: null,
				itemWrap: !0,
				itemTextFormatter: null
			},
			ToolTip: {
				enabled: !0,
				shared: !1,
				animationEnabled: !0,
				content: null,
				contentFormatter: null,
				reversed: !1,
				backgroundColor: null,
				borderColor: null,
				borderThickness: 2,
				cornerRadius: 5,
				fontSize: 14,
				fontColor: "#000000",
				fontFamily: "Calibri, Arial, Georgia, serif;",
				fontWeight: "normal",
				fontStyle: "italic"
			},
			Axis: {
				minimum: null,
				maximum: null,
				interval: null,
				intervalType: null,
				title: null,
				titleFontColor: "black",
				titleFontSize: 20,
				titleFontFamily: "arial",
				titleFontWeight: "normal",
				titleFontStyle: "normal",
				labelAngle: 0,
				labelFontFamily: "arial",
				labelFontColor: "black",
				labelFontSize: 12,
				labelFontWeight: "normal",
				labelFontStyle: "normal",
				labelAutoFit: !1,
				labelWrap: !0,
				labelMaxWidth: null,
				labelFormatter: null,
				prefix: "",
				suffix: "",
				includeZero: !0,
				tickLength: 5,
				tickColor: "black",
				tickThickness: 1,
				lineColor: "black",
				lineThickness: 1,
				lineDashType: "solid",
				gridColor: "A0A0A0",
				gridThickness: 0,
				gridDashType: "solid",
				interlacedColor: null,
				valueFormatString: null,
				margin: 2,
				stripLines: []
			},
			StripLine: {
				value: null,
				startValue: null,
				endValue: null,
				color: "orange",
				opacity: null,
				thickness: 2,
				lineDashType: "solid",
				label: "",
				labelBackgroundColor: "#EEEEEE",
				labelFontFamily: "arial",
				labelFontColor: "orange",
				labelFontSize: 12,
				labelFontWeight: "normal",
				labelFontStyle: "normal",
				labelFormatter: null,
				showOnTop: !1
			},
			DataSeries: {
				name: null,
				dataPoints: null,
				label: "",
				bevelEnabled: !1,
				highlightEnabled: !0,
				cursor: null,
				indexLabel: "",
				indexLabelPlacement: "auto",
				indexLabelOrientation: "horizontal",
				indexLabelFontColor: "black",
				indexLabelFontSize: 12,
				indexLabelFontStyle: "normal",
				indexLabelFontFamily: "Arial",
				indexLabelFontWeight: "normal",
				indexLabelBackgroundColor: null,
				indexLabelLineColor: null,
				indexLabelLineThickness: 1,
				indexLabelLineDashType: "solid",
				indexLabelMaxWidth: null,
				indexLabelWrap: !0,
				indexLabelFormatter: null,
				lineThickness: 2,
				lineDashType: "solid",
				color: null,
				risingColor: "white",
				fillOpacity: null,
				startAngle: 0,
				type: "column",
				xValueType: "number",
				axisYType: "primary",
				xValueFormatString: null,
				yValueFormatString: null,
				zValueFormatString: null,
				percentFormatString: null,
				showInLegend: null,
				legendMarkerType: null,
				legendMarkerColor: null,
				legendText: null,
				legendMarkerBorderColor: null,
				legendMarkerBorderThickness: null,
				markerType: "circle",
				markerColor: null,
				markerSize: null,
				markerBorderColor: null,
				markerBorderThickness: null,
				mouseover: null,
				mouseout: null,
				mousemove: null,
				click: null,
				toolTipContent: null,
				visible: !0
			},
			TextBlock: {
				x: 0,
				y: 0,
				width: null,
				height: null,
				maxWidth: null,
				maxHeight: null,
				padding: 0,
				angle: 0,
				text: "",
				horizontalAlign: "center",
				fontSize: 12,
				fontFamily: "calibri",
				fontWeight: "normal",
				fontColor: "black",
				fontStyle: "normal",
				borderThickness: 0,
				borderColor: "black",
				cornerRadius: 0,
				backgroundColor: null,
				textBaseline: "top"
			},
			CultureInfo: {
				decimalSeparator: ".",
				digitGroupSeparator: ",",
				zoomText: "Zoom",
				panText: "Pan",
				resetText: "Reset",
				menuText: "More Options",
				saveJPGText: "Save as JPG",
				savePNGText: "Save as PNG",
				days: ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
				shortDays: ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"],
				months: ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
				shortMonths: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"]
			}
		},
		ot = {
			en: {}
		},
		tt = {
			colorSet1: ["#369EAD", "#C24642", "#7F6084", "#86B402", "#A2D1CF", "#C8B631", "#6DBCEB", "#52514E", "#4F81BC", "#A064A1", "#F79647"],
			colorSet2: ["#4F81BC", "#C0504E", "#9BBB58", "#23BFAA", "#8064A1", "#4AACC5", "#F79647", "#33558B"],
			colorSet3: ["#8CA1BC", "#36845C", "#017E82", "#8CB9D0", "#708C98", "#94838D", "#F08891", "#0366A7", "#008276", "#EE7757", "#E5BA3A", "#F2990B", "#03557B", "#782970"]
		},
		ut = {
			theme1: {
				Chart: {
					colorSet: "colorSet1"
				},
				Title: {
					fontFamily: n ? "Calibri, Optima, Candara, Verdana, Geneva, sans-serif" : "calibri",
					fontSize: 33,
					fontColor: "#3A3A3A",
					fontWeight: "bold",
					verticalAlign: "top",
					margin: 5
				},
				Subtitle: {
					fontFamily: n ? "Calibri, Optima, Candara, Verdana, Geneva, sans-serif" : "calibri",
					fontSize: 16,
					fontColor: "#3A3A3A",
					fontWeight: "bold",
					verticalAlign: "top",
					margin: 5
				},
				Axis: {
					titleFontSize: 26,
					titleFontColor: "#666666",
					titleFontFamily: n ? "Calibri, Optima, Candara, Verdana, Geneva, sans-serif" : "calibri",
					labelFontFamily: n ? "Calibri, Optima, Candara, Verdana, Geneva, sans-serif" : "calibri",
					labelFontSize: 18,
					labelFontColor: "grey",
					tickColor: "#BBBBBB",
					tickThickness: 2,
					gridThickness: 2,
					gridColor: "#BBBBBB",
					lineThickness: 2,
					lineColor: "#BBBBBB"
				},
				Legend: {
					verticalAlign: "bottom",
					horizontalAlign: "center",
					fontFamily: n ? "monospace, sans-serif,arial black" : "calibri"
				},
				DataSeries: {
					indexLabelFontColor: "grey",
					indexLabelFontFamily: n ? "Calibri, Optima, Candara, Verdana, Geneva, sans-serif" : "calibri",
					indexLabelFontSize: 18,
					indexLabelLineThickness: 1
				}
			},
			theme2: {
				Chart: {
					colorSet: "colorSet2"
				},
				Title: {
					fontFamily: "impact, charcoal, arial black, sans-serif",
					fontSize: 32,
					fontColor: "#333333",
					verticalAlign: "top",
					margin: 5
				},
				Subtitle: {
					fontFamily: "impact, charcoal, arial black, sans-serif",
					fontSize: 14,
					fontColor: "#333333",
					verticalAlign: "top",
					margin: 5
				},
				Axis: {
					titleFontSize: 22,
					titleFontColor: "rgb(98,98,98)",
					titleFontFamily: n ? "monospace, sans-serif,arial black" : "arial",
					titleFontWeight: "bold",
					labelFontFamily: n ? "monospace, Courier New, Courier" : "arial",
					labelFontSize: 16,
					labelFontColor: "grey",
					labelFontWeight: "bold",
					tickColor: "grey",
					tickThickness: 2,
					gridThickness: 2,
					gridColor: "grey",
					lineColor: "grey",
					lineThickness: 0
				},
				Legend: {
					verticalAlign: "bottom",
					horizontalAlign: "center",
					fontFamily: n ? "monospace, sans-serif,arial black" : "arial"
				},
				DataSeries: {
					indexLabelFontColor: "grey",
					indexLabelFontFamily: n ? "Courier New, Courier, monospace" : "arial",
					indexLabelFontWeight: "bold",
					indexLabelFontSize: 18,
					indexLabelLineThickness: 1
				}
			},
			theme3: {
				Chart: {
					colorSet: "colorSet1"
				},
				Title: {
					fontFamily: n ? "Candara, Optima, Trebuchet MS, Helvetica Neue, Helvetica, Trebuchet MS, serif" : "calibri",
					fontSize: 32,
					fontColor: "#3A3A3A",
					fontWeight: "bold",
					verticalAlign: "top",
					margin: 5
				},
				Subtitle: {
					fontFamily: n ? "Candara, Optima, Trebuchet MS, Helvetica Neue, Helvetica, Trebuchet MS, serif" : "calibri",
					fontSize: 16,
					fontColor: "#3A3A3A",
					fontWeight: "bold",
					verticalAlign: "top",
					margin: 5
				},
				Axis: {
					titleFontSize: 22,
					titleFontColor: "rgb(98,98,98)",
					titleFontFamily: n ? "Verdana, Geneva, Calibri, sans-serif" : "calibri",
					labelFontFamily: n ? "Calibri, Optima, Candara, Verdana, Geneva, sans-serif" : "calibri",
					labelFontSize: 18,
					labelFontColor: "grey",
					tickColor: "grey",
					tickThickness: 2,
					gridThickness: 2,
					gridColor: "grey",
					lineThickness: 2,
					lineColor: "grey"
				},
				Legend: {
					verticalAlign: "bottom",
					horizontalAlign: "center",
					fontFamily: n ? "monospace, sans-serif,arial black" : "calibri"
				},
				DataSeries: {
					bevelEnabled: !0,
					indexLabelFontColor: "grey",
					indexLabelFontFamily: n ? "Candara, Optima, Calibri, Verdana, Geneva, sans-serif" : "calibri",
					indexLabelFontSize: 18,
					indexLabelLineColor: "lightgrey",
					indexLabelLineThickness: 2
				}
			}
		},
		f = {
			numberDuration: 1,
			yearDuration: 314496e5,
			monthDuration: 2592e6,
			weekDuration: 6048e5,
			dayDuration: 864e5,
			hourDuration: 36e5,
			minuteDuration: 6e4,
			secondDuration: 1e3,
			millisecondDuration: 1,
			dayOfWeekFromInt: ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]
		},
		hi = {},
		g = null,
		ii = function() {
			var n = /D{1,4}|M{1,4}|Y{1,4}|h{1,2}|H{1,2}|m{1,2}|s{1,2}|f{1,3}|t{1,2}|T{1,2}|K|z{1,3}|"[^"]*"|'[^']*'/g,
				t = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
				i = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"],
				r = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
				u = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
				f = /\b(?:[PMCEA][SDP]T|(?:Pacific|Mountain|Central|Eastern|Atlantic) (?:Standard|Daylight|Prevailing) Time|(?:GMT|UTC)(?:[-+]\d{4})?)\b/g,
				e = /[^-+\dA-Z]/g;
			return function(o, s, h) {
				var tt = h ? h.days : t,
					it = h ? h.months : r,
					rt = h ? h.shortDays : i,
					ut = h ? h.shortMonths : u,
					y = !1;
				if (o = o && o.getTime ? o : o ? new Date(o) : new Date, isNaN(o)) throw SyntaxError("invalid date");
				s.slice(0, 4) === "UTC:" && (s = s.slice(4), y = !0);
				var c = y ? "getUTC" : "get",
					k = o[c + "Date"](),
					d = o[c + "Day"](),
					p = o[c + "Month"](),
					w = o[c + "FullYear"](),
					l = o[c + "Hours"](),
					g = o[c + "Minutes"](),
					nt = o[c + "Seconds"](),
					b = o[c + "Milliseconds"](),
					a = y ? 0 : o.getTimezoneOffset();
				return s.replace(n, function(n) {
					switch (n) {
						case "D":
							return k;
						case "DD":
							return v(k, 2);
						case "DDD":
							return rt[d];
						case "DDDD":
							return tt[d];
						case "M":
							return p + 1;
						case "MM":
							return v(p + 1, 2);
						case "MMM":
							return ut[p];
						case "MMMM":
							return it[p];
						case "Y":
							return parseInt(String(w).slice(-2));
						case "YY":
							return v(String(w).slice(-2), 2);
						case "YYY":
							return v(String(w).slice(-3), 3);
						case "YYYY":
							return v(w, 4);
						case "h":
							return l % 12 || 12;
						case "hh":
							return v(l % 12 || 12, 2);
						case "H":
							return l;
						case "HH":
							return v(l, 2);
						case "m":
							return g;
						case "mm":
							return v(g, 2);
						case "s":
							return nt;
						case "ss":
							return v(nt, 2);
						case "f":
							return String(b).slice(0, 1);
						case "ff":
							return v(String(b).slice(0, 2), 2);
						case "fff":
							return v(String(b).slice(0, 3), 3);
						case "t":
							return l < 12 ? "a" : "p";
						case "tt":
							return l < 12 ? "am" : "pm";
						case "T":
							return l < 12 ? "A" : "P";
						case "TT":
							return l < 12 ? "AM" : "PM";
						case "K":
							return y ? "UTC" : (String(o).match(f) || [""]).pop().replace(e, "");
						case "z":
							return (a > 0 ? "-" : "+") + Math.floor(Math.abs(a) / 60);
						case "zz":
							return (a > 0 ? "-" : "+") + v(Math.floor(Math.abs(a) / 60), 2);
						case "zzz":
							return (a > 0 ? "-" : "+") + v(Math.floor(Math.abs(a) / 60), 2) + v(Math.abs(a) % 60, 2);
						default:
							return n.slice(1, n.length - 1)
					}
				})
			}
		}(),
		it = function(n, t, i) {
			var w, r, e, nt, s, ft;
			if (n === null) return "";
			n = Number(n);
			w = n < 0 ? !0 : !1;
			w && (n *= -1);
			var at = i ? i.decimalSeparator : ".",
				b = i ? i.digitGroupSeparator : ",",
				ot = "";
			t = String(t);
			var a = 1,
				u = "",
				y = "",
				h = -1,
				k = [],
				d = [],
				p = 0,
				st = 0,
				g = 0,
				ht = !1,
				c = 0;
			for (y = t.match(/"[^"]*"|'[^']*'|[eE][+-]*[0]+|[,]+[.]|‰|./g), r = null, e = 0; y && e < y.length; e++) {
				if (r = y[e], r === "." && h < 0) {
					h = e;
					continue
				} else if (r === "%") a *= 100;
				else if (r === "‰") {
					a *= 1e3;
					continue
				} else if (r[0] === "," && r[r.length - 1] === ".") {
					a /= Math.pow(1e3, r.length - 1);
					h = e + r.length - 1;
					continue
				} else(r[0] === "E" || r[0] === "e") && r[r.length - 1] === "0" && (ht = !0);
				h < 0 ? (k.push(r), r === "#" || r === "0" ? p++ : r === "," && g++) : (d.push(r), (r === "#" || r === "0") && st++)
			}
			ht && (nt = Math.floor(n), c = (nt === 0 ? "" : String(nt)).length - p, a /= Math.pow(10, c));
			n *= a;
			h < 0 && (h = e);
			ot = n.toFixed(st);
			var ct = ot.split("."),
				f = (ct[0] + "").split(""),
				tt = (ct[1] + "").split("");
			f && f[0] === "0" && f.shift();
			for (var lt = 0, it = 0, rt = 0, ut = 0, o = 0; k.length > 0;)
				if (r = k.pop(), r === "#" || r === "0")
					if (lt++, lt === p) {
						if (s = f, f = [], r === "0")
							for (ft = p - it - (s ? s.length : 0); ft > 0;) s.unshift("0"), ft--;
						while (s.length > 0) u = s.pop() + u, o++, o % ut == 0 && rt === g && s.length > 0 && (u = b + u);
						w && (u = "-" + u)
					} else f.length > 0 ? (u = f.pop() + u, it++, o++) : r === "0" && (u = "0" + u, it++, o++), o % ut == 0 && rt === g && f.length > 0 && (u = b + u);
			else(r[0] === "E" || r[0] === "e") && r[r.length - 1] === "0" && /[eE][+-]*[0]+/.test(r) ? (r = c < 0 ? r.replace("+", "").replace("-", "") : r.replace("-", ""), u += r.replace(/[0]+/, function(n) {
				return v(c, n.length)
			})) : r === "," ? (rt++, ut = o, o = 0, f.length > 0 && (u = b + u)) : u = r.length > 1 && (r[0] === '"' && r[r.length - 1] === '"' || r[0] === "'" && r[r.length - 1] === "'") ? r.slice(1, r.length - 1) + u : r + u;
			for (var l = "", et = !1; d.length > 0;) r = d.shift(), r === "#" || r === "0" ? tt.length > 0 && Number(tt.join("")) !== 0 ? (l += tt.shift(), et = !0) : r === "0" && (l += "0", et = !0) : r.length > 1 && (r[0] === '"' && r[r.length - 1] === '"' || r[0] === "'" && r[r.length - 1] === "'") ? l += r.slice(1, r.length - 1) : (r[0] === "E" || r[0] === "e") && r[r.length - 1] === "0" && /[eE][+-]*[0]+/.test(r) ? (r = c < 0 ? r.replace("+", "").replace("-", "") : r.replace("-", ""), l += r.replace(/[0]+/, function(n) {
				return v(c, n.length)
			})) : l += r;
			return u + ((et ? at : "") + l)
		},
		wt = function(n) {
			var t = 0,
				i = 0;
			return n = n || window.event, n.offsetX || n.offsetX === 0 ? (t = n.offsetX, i = n.offsetY) : n.layerX || n.layerX == 0 ? (t = n.layerX, i = n.layerY) : (t = n.pageX - n.target.offsetLeft, i = n.pageY - n.target.offsetTop), {
				x: t,
				y: i
			}
		},
		li = !0,
		ri = window.devicePixelRatio || 1,
		bt = 1,
		l = li ? ri / bt : 1,
		nr = {
			reset: {
				image: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAcCAYAAAAAwr0iAAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAALEgAACxIB0t1+/AAAABx0RVh0U29mdHdhcmUAQWRvYmUgRmlyZXdvcmtzIENTNui8sowAAAKRSURBVEiJrdY/iF1FFMfxzwnZrGISUSR/JLGIhoh/QiRNBLWxMLIWEkwbgiAoFgoW2mhlY6dgpY2IlRBRxBSKhSAKIklWJRYuMZKAhiyopAiaTY7FvRtmZ+/ed9/zHRjezLw5v/O9d86cuZGZpmURAfdn5o9DfdZNLXpjz+LziPgyIl6MiG0jPTJzZBuyDrP4BVm0P/AKbljTb4ToY/gGewYA7KyCl+1b3DUYANvwbiHw0gCAGRzBOzjTAXEOu0cC4Ch+r5x/HrpdrcZmvIDFSucMtnYCYC++6HmNDw8FKDT34ETrf639/azOr5vwRk/g5fbeuABtgC04XWk9VQLciMP4EH/3AFzErRNC7MXlQmsesSoHsGPE23hmEoBW+61K66HMXFmIMvN8myilXS36R01ub+KfYvw43ZXwYDX+AHP4BAci4pFJomfmr/ihmNofESsBImJGk7mlncrM45n5JPbhz0kAWpsv+juxaX21YIPmVJS2uNzJMS6ZNexC0d+I7fUWXLFyz2kSZlpWPvASlmqAf/FXNXf3FAF2F/1LuFifAlionB6dRuSI2IwHi6lzmXmp6xR8XY0fiIh7psAwh+3FuDkRHQVjl+a8lkXjo0kLUKH7XaV5oO86PmZ1FTzyP4K/XGl9v/zwfbW7BriiuETGCP5ch9bc9f97HF/vcFzCa5gdEPgWq+t/4v0V63oE1uF4h0DiFJ7HnSWMppDdh1dxtsPvJ2wcBNAKbsJXa0Ck5opdaBPsRNu/usba09i1KsaAVzmLt3sghrRjuK1Tf4xkegInxwy8gKf7dKMVH2QRsV5zXR/Cftyu+aKaKbbkQrsdH+PTzLzcqzkOQAVzM+7FHdiqqe2/YT4zF/t8S/sPmawyvC974vcAAAAASUVORK5CYII="
			},
			pan: {
				image: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAALEgAACxIB0t1+/AAAABx0RVh0U29mdHdhcmUAQWRvYmUgRmlyZXdvcmtzIENTNui8sowAAAJVSURBVFiFvZe7a1RBGMV/x2hWI4JpfKCIiSBKOoOCkID/wP4BFqIIFkE02ChIiC8QDKlSiI3YqRBsBVGwUNAUdiIEUgjiAzQIIsuKJsfizsXr5t7d+8jmwLDfzHz3nLOzc7+ZxTZlGyDgZiWOCuJ9wH2gCUyuqQFgF/AGcKJNrYkBYBj40CIet+muGQi/96kM4WS7C/Tm5VUg7whJg8BkEGkCR4BDYfodsADUgP6wErO5iCtswsuJb32hdbXy8qzL5TIdmzJinHdZoZIBZcSFkGlAKs1Z3YCketZcBtouuaQNkrblMiBpBrhme7mAgU4wMCvpcFsDkq4C54DFVRTH9h+i6vlE0r5UA5ImgCuh28jB28iIs7BIVCOeStoZD64P4uPAjUTygKSx2FsK2TIwkugfk9Qkfd/E+yMWHQCeSRqx/R3gOp3LazfaS2C4B5gHDgD7U9x3E3uAH7KNpC3AHHAwTL4FHgM9GQ8vAaPA0dB/Abxqk2/gBLA9MXba9r1k/d4LfA3JtwueBeM58ucS+edXnAW23wP10N3advEi9CXizTnyN4bPS7Zn4sH/dq3t18AY4e1YLYSy3g/csj2VnFshZPuOpOeSKHCodUINuGj7YetE6je1PV9QoNPJ9StNHKodx7nRbiWrGHBGXAi5DUiqtQwtpcWK0Jubt8CltA5MEV1IfwO7+VffPwGfia5m34CT4bXujIIX0Qna1/cGMNqV/wUJE2czxD8CQ4X5Sl7Jz7SILwCDpbjKPBRMHAd+EtX4HWV5Spdc2w8kDQGPbH8py/MXMygM69/FKz4AAAAASUVORK5CYII="
			},
			zoom: {
				image: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAAK6wAACusBgosNWgAAABx0RVh0U29mdHdhcmUAQWRvYmUgRmlyZXdvcmtzIENTNui8sowAAAMqSURBVFiFvdfbj91TFMDxz57U6GUEMS1aYzyMtCSSDhWjCZMInpAI3khE/QHtgzdRkXgSCS8SES9epKLi0oRKNETjRahREq2KS1stdRujtDPtbA97n5zdn9+5zJxTK9k5v3POXmt991p7r71+IcaoGwkhTOIebMRqzOBTvIG3Y4zTXRmqSoyx5cAKbMJOHMFJnMZ8/jyFaXyMR7G6nb1aH22cP4BvcBxziG3GKfyTIR9D6BYg1KUghPBCDveFlb/24Av8iuUYw41YVsz5G7uxKcZ4aMEpwGt5NY3V/YbHsQ6rcAHOw/kYxigewr5CZw4fYGxBKcCLOFEYehXrMdRhr5yLETxVScsOLOkKAPfn1TYMPIvLFrShUlS2FDZm8XRHACzFAWl3R2xbqPMCYhmeLCAOYEMngAczbcTvuHYxzguIy/FesR9e6gSwU/OoPYHBHgHgviIKX2Flq7k34KhmcVnbi/PC8JX4MgMcxb118wZwdz5aISscqx7VRcox7MrPQ7i+btIAJrAkf9+bI9EPmZY2IAxiTSuAldLq4Y9+AcSUh78KP0tbAcwU35cXMD1JCIFUoGiehlqAz6TNB1f1C0DK+0h+nsNPrQC2a4bqGmlD9kOGcWt+Po6pVgDvSxfJaSkFd4UQBvoAsBYbCoB3a2flM7slA0R8iyt6rAFDeDPbm8eOTpVwGD9qVq7nLbIaZnmksPU1JtsCZMXNmpdRxFasWITzh6Xj3LCzra1OxcD2QjHiGVzdpfORnMqZio2PcF23ABdJF1Np4BPptlyPi6WzPYBzpJZtHe7A6xW9cnyP8TqA//SEIYRL8Bxul7rihvwgtVn78WcGGZXa9HGd5TDujDHuOePXNiHdKjWgZX/YbsxLx/ktqbjVzTlcjUSnvI5JrdlUVp6WesZZ6R1hRrpq9+EVTGS9jTjYAuKIouGpbcurEkIYxC051KNSamazsc+xK8b4S0VnEi/j0hqTP+M27O258egQwZuzs7pI7Mf4WQXIEDc5s9sux+5+1Py2EmP8UOq6GvWhIScxfdYjUERiAt9Jd84J6a16zf8JEKT3yCm8g1UxRv8CC4pyRhzR1uUAAAAASUVORK5CYII="
			},
			menu: {
				image: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAgCAYAAAAbifjMAAAABHNCSVQICAgIfAhkiAAAAAlwSFlzAAAK6wAACusBgosNWgAAABx0RVh0U29mdHdhcmUAQWRvYmUgRmlyZXdvcmtzIENTNui8sowAAAAWdEVYdENyZWF0aW9uIFRpbWUAMDcvMTUvMTTPsvU0AAAAP0lEQVRIie2SMQoAIBDDUvH/X667g8sJJ9KOhYYOkW0qGaU1MPdC0vGSbV19EACo3YMPAFH5BUBUjsqfAPpVXtNgGDfxEDCtAAAAAElFTkSuQmCC"
			}
		},
		o, dt;
	h.prototype.setOptions = function(n, t) {
		var r, i;
		if (et[this._defaultsKey]) {
			r = et[this._defaultsKey];
			for (i in r) this[i] = n && i in n ? n[i] : t && i in t ? t[i] : r[i]
		} else yt && window.console && console.log("defaults not set")
	};
	h.prototype.updateOption = function(n) {
		!et[this._defaultsKey] && yt && window.console && console.log("defaults not set");
		var u = et[this._defaultsKey],
			t = this._options.theme ? this._options.theme : this.chart && this.chart._options.theme ? this.chart._options.theme : "theme1",
			i = {},
			r = this[n];
		return (t && ut[t] && ut[t][this._defaultsKey] && (i = ut[t][this._defaultsKey]), n in u && (r = n in this._options ? this._options[n] : i && n in i ? i[n] : u[n]), r === this[n]) ? !1 : (this[n] = r, !0)
	};
	h.prototype.trackChanges = function(n) {
		this._options._oldOptions || (this._options._oldOptions = {});
		this._options._oldOptions[n] = this._options[n]
	};
	h.prototype.isBeingTracked = function(n) {
		return this._options._oldOptions || (this._options._oldOptions = {}), this._options._oldOptions[n] ? !0 : !1
	};
	h.prototype.hasOptionChanged = function(n) {
		this._options._oldOptions || (this._options._oldOptions = {});
		return !(this._options._oldOptions[n] === this._options[n])
	};
	h.prototype.addEventListener = function(n, t, i) {
		n && t && (i = i || this, this._eventListeners[n] = this._eventListeners[n] || [], this._eventListeners[n].push({
			context: i,
			eventHandler: t
		}))
	};
	h.prototype.removeEventListener = function(n, t) {
		var r, i;
		if (n && t && this._eventListeners[n])
			for (r = this._eventListeners[n], i = 0; i < r.length; i++)
				if (r[i].eventHandler === t) {
					r[i].splice(i, 1);
					break
				}
	};
	h.prototype.removeAllEventListeners = function() {
		this._eventListeners = []
	};
	h.prototype.dispatchEvent = function(n, t) {
		var r, i;
		if (n && this._eventListeners[n])
			for (t = t || {}, r = this._eventListeners[n], i = 0; i < r.length; i++) r[i].eventHandler.call(r[i].context, t)
	};
	w(t, h);
	t.prototype._updateOptions = function() {
		var t = this,
			i, u, f, r;
		this.updateOption("width");
		this.updateOption("height");
		this.updateOption("theme");
		this.updateOption("colorSet") && (this._selectedColorSet = typeof tt[this.colorSet] != "undefined" ? tt[this.colorSet] : tt.colorSet1);
		this.updateOption("backgroundColor");
		this.backgroundColor || (this.backgroundColor = "rgba(0,0,0,0)");
		this.updateOption("culture");
		this._cultureInfo = new vt(this._options.culture);
		this.updateOption("animationEnabled");
		this.animationEnabled = this.animationEnabled && n;
		this._options.zoomEnabled ? (this._zoomButton || (nt(this._zoomButton = document.createElement("button")), b(this, this._zoomButton, "pan"), this._toolBar.appendChild(this._zoomButton), s(this._zoomButton, "click", function() {
			t.zoomEnabled ? (t.zoomEnabled = !1, t.panEnabled = !0, b(t, t._zoomButton, "zoom")) : (t.zoomEnabled = !0, t.panEnabled = !1, b(t, t._zoomButton, "pan"));
			t.render()
		})), this._resetButton || (nt(this._resetButton = document.createElement("button")), b(this, this._resetButton, "reset"), this._toolBar.appendChild(this._resetButton), s(this._resetButton, "click", function() {
			t._toolTip.hide();
			t.zoomEnabled || t.panEnabled ? (t.zoomEnabled = !0, t.panEnabled = !1, b(t, t._zoomButton, "pan"), t._defaultCursor = "default", t.overlaidCanvas.style.cursor = t._defaultCursor) : (t.zoomEnabled = !1, t.panEnabled = !1);
			t.sessionVariables.axisX.internalMinimum = t._options.axisX && t._options.axisX.minimum ? t._options.axisX.minimum : null;
			t.sessionVariables.axisX.internalMaximum = t._options.axisX && t._options.axisX.maximum ? t._options.axisX.maximum : null;
			t.resetOverlayedCanvas();
			nt(t._zoomButton, t._resetButton);
			t.render()
		}), this.overlaidCanvas.style.cursor = t._defaultCursor), this.zoomEnabled || this.panEnabled || (this._zoomButton ? (t._zoomButton.getAttribute("state") === t._cultureInfo.zoomText ? (this.panEnabled = !0, this.zoomEnabled = !1) : (this.zoomEnabled = !0, this.panEnabled = !1), ui(t._zoomButton, t._resetButton)) : (this.zoomEnabled = !0, this.panEnabled = !1))) : (this.zoomEnabled = !1, this.panEnabled = !1);
		typeof this._options.exportFileName != "undefined" && (this.exportFileName = this._options.exportFileName);
		typeof this._options.exportEnabled != "undefined" && (this.exportEnabled = this._options.exportEnabled);
		this._menuButton ? this.exportEnabled ? ui(this._menuButton) : nt(this._menuButton) : this.exportEnabled && n && (this._menuButton = document.createElement("button"), b(this, this._menuButton, "menu"), this._toolBar.appendChild(this._menuButton), s(this._menuButton, "click", function() {
			if (t._dropdownMenu.style.display === "none") {
				if (t._dropDownCloseTime && (new Date).getTime() - t._dropDownCloseTime.getTime() <= 500) return;
				t._dropdownMenu.style.display = "block";
				t._menuButton.blur();
				t._dropdownMenu.focus()
			}
		}, !0));
		!this._dropdownMenu && this.exportEnabled && n && (this._dropdownMenu = document.createElement("div"), this._dropdownMenu.setAttribute("tabindex", -1), this._dropdownMenu.style.cssText = "position: absolute; -webkit-user-select: none; -moz-user-select: none; -ms-user-select: none; user-select: none; cursor: pointer;right: 1px;top: 25px;min-width: 120px;outline: 0;border: 1px solid silver;font-size: 14px;font-family: Calibri, Verdana, sans-serif;padding: 5px 0px 5px 0px;text-align: left;background-color: #fff;line-height: 20px;box-shadow: 2px 2px 10px #888888;", t._dropdownMenu.style.display = "none", this._toolBar.appendChild(this._dropdownMenu), s(this._dropdownMenu, "blur", function() {
			nt(t._dropdownMenu);
			t._dropDownCloseTime = new Date
		}, !0), i = document.createElement("div"), i.style.cssText = "padding: 2px 15px 2px 10px", i.innerHTML = this._cultureInfo.saveJPGText, this._dropdownMenu.appendChild(i), s(i, "mouseover", function() {
			this.style.backgroundColor = "#EEEEEE"
		}, !0), s(i, "mouseout", function() {
			this.style.backgroundColor = "transparent"
		}, !0), s(i, "click", function() {
			ai(t.canvas, "jpg", t.exportFileName);
			nt(t._dropdownMenu)
		}, !0), i = document.createElement("div"), i.style.cssText = "padding: 2px 15px 2px 10px", i.innerHTML = this._cultureInfo.savePNGText, this._dropdownMenu.appendChild(i), s(i, "mouseover", function() {
			this.style.backgroundColor = "#EEEEEE"
		}, !0), s(i, "mouseout", function() {
			this.style.backgroundColor = "transparent"
		}, !0), s(i, "click", function() {
			ai(t.canvas, "png", t.exportFileName);
			nt(t._dropdownMenu)
		}, !0));
		this._toolBar.style.display !== "none" && this._zoomButton && (this.panEnabled ? b(t, t._zoomButton, "zoom") : b(t, t._zoomButton, "pan"), t._resetButton.getAttribute("state") !== t._cultureInfo.resetText && b(t, t._resetButton, "reset"));
		typeof et.Chart.creditHref == "undefined" ? (this.creditHref = "http://canvasjs.com/", this.creditText = "CanvasJS.com") : (u = this.updateOption("creditText"), f = this.updateOption("creditHref"));
		(this.renderCount === 0 || u || f) && (this._creditLink.setAttribute("href", this.creditHref), this._creditLink.innerHTML = this.creditText);
		this.creditHref && this.creditText ? this._creditLink.parentElement || this._canvasJSContainer.appendChild(this._creditLink) : this._creditLink.parentElement && this._canvasJSContainer.removeChild(this._creditLink);
		this._options.toolTip && this._toolTip._options !== this._options.toolTip && (this._toolTip._options = this._options.toolTip);
		for (r in this._toolTip._options) this._toolTip._options.hasOwnProperty(r) && this._toolTip.updateOption(r)
	};
	t.prototype._updateSize = function() {
		var n = 0,
			t = 0;
		return (this._options.width ? n = this.width : this.width = n = this._container.clientWidth > 0 ? this._container.clientWidth : this.width, this._options.height ? t = this.height : this.height = t = this._container.clientHeight > 0 ? this._container.clientHeight : this.height, this.canvas.width !== n * l || this.canvas.height !== t * l) ? (ct(this.canvas, n, t), ct(this.overlaidCanvas, n, t), ct(this._eventManager.ghostCanvas, n, t), !0) : !1
	};
	t.prototype._initialize = function() {
		var f, u, i, e, r;
		for (this._animator ? this._animator.cancelAllAnimations() : this._animator = new ei(this), this.removeAllEventListeners(), this.disableToolTip = !1, this.pieDoughnutClickHandler = null, this.animationRequestId && this.cancelRequestAnimFrame.call(window, this.animationRequestId), this._updateOptions(), this.animatedRender = n && this.animationEnabled && this.renderCount === 0, this._updateSize(), this.clearCanvas(), this.ctx.beginPath(), this.axisX = null, this.axisY = null, this.axisY2 = null, this._indexLabels = [], this._dataInRenderedOrder = [], this._events = [], this._eventManager && this._eventManager.reset(), this.plotInfo = {
				axisPlacement: null,
				axisXValueType: null,
				plotTypes: []
			}, this.layoutManager = new ft(0, 0, this.width, this.height, 2), this.plotArea.layoutManager && this.plotArea.layoutManager.reset(), this.data = [], f = 0, u = 0; u < this._options.data.length; u++)
			if ((f++, !this._options.data[u].type || t._supportedChartTypes.indexOf(this._options.data[u].type) >= 0) && (i = new d(this, this._options.data[u], this.theme, f - 1, ++this._eventManager.lastObjectId), i.name === null && (i.name = "DataSeries " + f), i.color === null ? this._options.data.length > 1 ? (i._colorSet = [this._selectedColorSet[i.index % this._selectedColorSet.length]], i.color = this._selectedColorSet[i.index % this._selectedColorSet.length]) : i._colorSet = i.type === "line" || i.type === "stepLine" || i.type === "spline" || i.type === "area" || i.type === "stepArea" || i.type === "splineArea" || i.type === "stackedArea" || i.type === "stackedArea100" || i.type === "rangeArea" || i.type === "rangeSplineArea" || i.type === "candlestick" || i.type === "ohlc" ? [this._selectedColorSet[0]] : this._selectedColorSet : i._colorSet = [i.color], i.markerSize === null && ((i.type === "line" || i.type === "stepLine" || i.type === "spline") && i.dataPoints && i.dataPoints.length < this.width / 16 || i.type === "scatter") && (i.markerSize = 8), (i.type === "bubble" || i.type === "scatter") && i.dataPoints && i.dataPoints.sort(wi), this.data.push(i), e = i.axisPlacement, e === "normal" ? this.plotInfo.axisPlacement === "xySwapped" ? r = 'You cannot combine "' + i.type + '" with bar chart' : this.plotInfo.axisPlacement === "none" ? r = 'You cannot combine "' + i.type + '" with pie chart' : this.plotInfo.axisPlacement === null && (this.plotInfo.axisPlacement = "normal") : e === "xySwapped" ? this.plotInfo.axisPlacement === "normal" ? r = 'You cannot combine "' + i.type + '" with line, area, column or pie chart' : this.plotInfo.axisPlacement === "none" ? r = 'You cannot combine "' + i.type + '" with pie chart' : this.plotInfo.axisPlacement === null && (this.plotInfo.axisPlacement = "xySwapped") : e == "none" && (this.plotInfo.axisPlacement === "normal" ? r = 'You cannot combine "' + i.type + '" with line, area, column or bar chart' : this.plotInfo.axisPlacement === "xySwapped" ? r = 'You cannot combine "' + i.type + '" with bar chart' : this.plotInfo.axisPlacement === null && (this.plotInfo.axisPlacement = "none")), r && window.console)) {
				window.console.log(r);
				return
			} this._objectsInitialized = !0
	};
	t._supportedChartTypes = di(["line", "stepLine", "spline", "column", "area", "stepArea", "splineArea", "bar", "bubble", "scatter", "stackedColumn", "stackedColumn100", "stackedBar", "stackedBar100", "stackedArea", "stackedArea100", "candlestick", "ohlc", "rangeColumn", "rangeBar", "rangeArea", "rangeSplineArea", "pie", "doughnut", "funnel"]);
	t.prototype.render = function(n) {
		var o, s, v, f, h, p, u, y, c, t, i, a, w, b, r;
		for (n && (this._options = n), this._initialize(), o = [], u = 0; u < this.data.length; u++)(this.plotInfo.axisPlacement === "normal" || this.plotInfo.axisPlacement === "xySwapped") && (this.data[u].axisYType && this.data[u].axisYType !== "primary" ? this.data[u].axisYType === "secondary" && (this.axisY2 || (this.plotInfo.axisPlacement === "normal" ? this.axisY2 = new e(this, this._options.axisY2, "axisY", "right") : this.plotInfo.axisPlacement === "xySwapped" && (this.axisY2 = new e(this, this._options.axisY2, "axisY", "top"))), this.data[u].axisY = this.axisY2) : (this.axisY || (this.plotInfo.axisPlacement === "normal" ? this.axisY = new e(this, this._options.axisY, "axisY", "left") : this.plotInfo.axisPlacement === "xySwapped" && (this.axisY = new e(this, this._options.axisY, "axisY", "bottom"))), this.data[u].axisY = this.axisY), this.axisX || (this.plotInfo.axisPlacement === "normal" ? this.axisX = new e(this, this._options.axisX, "axisX", "bottom") : this.plotInfo.axisPlacement === "xySwapped" && (this.axisX = new e(this, this._options.axisX, "axisX", "left"))), this.data[u].axisX = this.axisX);
		if (this._processData(), this._options.title && (this._title = new lt(this, this._options.title), this._title.dockInsidePlotArea ? o.push(this._title) : this._title.render()), this._options.subtitles)
			for (u = 0; u < this._options.subtitles.length; u++) this.subtitles = [], s = new gt(this, this._options.subtitles[u]), this.subtitles.push(s), s.dockInsidePlotArea ? o.push(s) : s.render();
		for (this.legend = new ni(this, this._options.legend, this.theme), u = 0; u < this.data.length; u++)(this.data[u].showInLegend || this.data[u].type === "pie" || this.data[u].type === "doughnut") && this.legend.dataSeries.push(this.data[u]);
		if (this.legend.dockInsidePlotArea ? o.push(this.legend) : this.legend.render(), this.plotInfo.axisPlacement === "normal" || this.plotInfo.axisPlacement === "xySwapped") e.setLayoutAndRender(this.axisX, this.axisY, this.axisY2, this.plotInfo.axisPlacement, this.layoutManager.getFreeSpace());
		else if (this.plotInfo.axisPlacement === "none") this.preparePlotArea();
		else return;
		v = 0;
		for (v in o) o[v].render();
		for (f = [], this.animatedRender && (h = rt(this.width, this.height), p = h.getContext("2d"), p.drawImage(this.canvas, 0, 0, this.width, this.height)), u = 0; u < this.plotInfo.plotTypes.length; u++)
			for (y = this.plotInfo.plotTypes[u], c = 0; c < y.plotUnits.length; c++) {
				for (t = y.plotUnits[c], i = null, t.targetCanvas = null, this.animatedRender && (t.targetCanvas = rt(this.width, this.height), t.targetCanvasCtx = t.targetCanvas.getContext("2d")), t.type === "line" ? i = this.renderLine(t) : t.type === "stepLine" ? i = this.renderStepLine(t) : t.type === "spline" ? i = this.renderSpline(t) : t.type === "column" ? i = this.renderColumn(t) : t.type === "bar" ? i = this.renderBar(t) : t.type === "area" ? i = this.renderArea(t) : t.type === "stepArea" ? i = this.renderStepArea(t) : t.type === "splineArea" ? i = this.renderSplineArea(t) : t.type === "stackedColumn" ? i = this.renderStackedColumn(t) : t.type === "stackedColumn100" ? i = this.renderStackedColumn100(t) : t.type === "stackedBar" ? i = this.renderStackedBar(t) : t.type === "stackedBar100" ? i = this.renderStackedBar100(t) : t.type === "stackedArea" ? i = this.renderStackedArea(t) : t.type === "stackedArea100" ? i = this.renderStackedArea100(t) : t.type === "bubble" ? i = i = this.renderBubble(t) : t.type === "scatter" ? i = this.renderScatter(t) : t.type === "pie" ? this.renderPie(t) : t.type === "doughnut" ? this.renderPie(t) : t.type === "candlestick" ? i = this.renderCandlestick(t) : t.type === "ohlc" ? i = this.renderCandlestick(t) : t.type === "rangeColumn" ? i = this.renderRangeColumn(t) : t.type === "rangeBar" ? i = this.renderRangeBar(t) : t.type === "rangeArea" ? i = this.renderRangeArea(t) : t.type === "rangeSplineArea" && (i = this.renderRangeSplineArea(t)), a = 0; a < t.dataSeriesIndexes.length; a++) this._dataInRenderedOrder.push(this.data[t.dataSeriesIndexes[a]]);
				this.animatedRender && i && f.push(i)
			}
		this.animatedRender && this._indexLabels.length > 0 && (w = rt(this.width, this.height), b = w.getContext("2d"), f.push(this.renderIndexLabels(b)));
		r = this;
		f.length > 0 ? (r.disableToolTip = !0, r._animator.animate(200, r.animationDuration, function(n) {
			r.ctx.clearRect(0, 0, r.width, r.height);
			r.ctx.drawImage(h, 0, 0, Math.floor(r.width * l), Math.floor(r.height * l), 0, 0, r.width, r.height);
			for (var t = 0; t < f.length; t++) i = f[t], n < 1 && typeof i.startTimePercent != "undefined" ? n >= i.startTimePercent && i.animationCallback(i.easingFunction(n - i.startTimePercent, 0, 1, 1 - i.startTimePercent), i) : i.animationCallback(i.easingFunction(n, 0, 1, 1), i);
			r.dispatchEvent("dataAnimationIterationEnd", {
				chart: r
			})
		}, function() {
			var e, n, i, t, u;
			for (f = [], e = 0, n = 0; n < r.plotInfo.plotTypes.length; n++)
				for (i = r.plotInfo.plotTypes[n], t = 0; t < i.plotUnits.length; t++) u = i.plotUnits[t], u.targetCanvas = null;
			h = null;
			r.disableToolTip = !1
		})) : (r._indexLabels.length > 0 && r.renderIndexLabels(), r.dispatchEvent("dataAnimationIterationEnd", {
			chart: r
		}));
		this.attachPlotAreaEventHandlers();
		this.zoomEnabled || this.panEnabled || !this._zoomButton || this._zoomButton.style.display === "none" || nt(this._zoomButton, this._resetButton);
		this._toolTip._updateToolTip();
		this.renderCount++;
		yt && (r = this, setTimeout(function() {
			var n = document.getElementById("ghostCanvasCopy"),
				t;
			n && (ct(n, r.width, r.height), t = n.getContext("2d"), t.drawImage(r._eventManager.ghostCanvas, 0, 0))
		}, 2e3))
	};
	t.prototype.attachPlotAreaEventHandlers = function() {
		this.attachEvent({
			context: this,
			chart: this,
			mousedown: this._plotAreaMouseDown,
			mouseup: this._plotAreaMouseUp,
			mousemove: this._plotAreaMouseMove,
			cursor: this.zoomEnabled ? "col-resize" : "move",
			cursor: this.panEnabled ? "move" : "default",
			capture: !0,
			bounds: this.plotArea
		})
	};
	t.prototype.categoriseDataSeries = function() {
		for (var f, i, e, n, r = "", u = 0; u < this.data.length; u++)
			if ((r = this.data[u], r.dataPoints && r.dataPoints.length !== 0 && r.visible) && t._supportedChartTypes.indexOf(r.type) >= 0) {
				var i = null,
					o = !1,
					f = null,
					s = !1;
				for (n = 0; n < this.plotInfo.plotTypes.length; n++)
					if (this.plotInfo.plotTypes[n].type === r.type) {
						o = !0;
						i = this.plotInfo.plotTypes[n];
						break
					} for (o || (i = {
						type: r.type,
						totalDataSeries: 0,
						plotUnits: []
					}, this.plotInfo.plotTypes.push(i)), n = 0; n < i.plotUnits.length; n++)
					if (i.plotUnits[n].axisYType === r.axisYType) {
						s = !0;
						f = i.plotUnits[n];
						break
					} s || (f = {
					type: r.type,
					previousDataSeriesCount: 0,
					index: i.plotUnits.length,
					plotType: i,
					axisYType: r.axisYType,
					axisY: r.axisYType === "primary" ? this.axisY : this.axisY2,
					axisX: this.axisX,
					dataSeriesIndexes: [],
					yTotals: []
				}, i.plotUnits.push(f));
				i.totalDataSeries++;
				f.dataSeriesIndexes.push(u);
				r.plotUnit = f
			} for (u = 0; u < this.plotInfo.plotTypes.length; u++)
			for (i = this.plotInfo.plotTypes[u], e = 0, n = 0; n < i.plotUnits.length; n++) i.plotUnits[n].previousDataSeriesCount = e, e += i.plotUnits[n].dataSeriesIndexes.length
	};
	t.prototype.assignIdToDataPoints = function() {
		for (var t, r, i, n = 0; n < this.data.length; n++)
			if (t = this.data[n], t.dataPoints)
				for (r = t.dataPoints.length, i = 0; i < r; i++) t.dataPointIds[i] = ++this._eventManager.lastObjectId
	};
	t.prototype._processData = function() {
		var t, r, i, n;
		for (this.assignIdToDataPoints(), this.categoriseDataSeries(), t = 0; t < this.plotInfo.plotTypes.length; t++)
			for (r = this.plotInfo.plotTypes[t], i = 0; i < r.plotUnits.length; i++) n = r.plotUnits[i], n.type === "line" || n.type === "stepLine" || n.type === "spline" || n.type === "column" || n.type === "area" || n.type === "stepArea" || n.type === "splineArea" || n.type === "bar" || n.type === "bubble" || n.type === "scatter" ? this._processMultiseriesPlotUnit(n) : n.type === "stackedColumn" || n.type === "stackedBar" || n.type === "stackedArea" ? this._processStackedPlotUnit(n) : n.type === "stackedColumn100" || n.type === "stackedBar100" || n.type === "stackedArea100" ? this._processStacked100PlotUnit(n) : (n.type === "candlestick" || n.type === "ohlc" || n.type === "rangeColumn" || n.type === "rangeBar" || n.type === "rangeArea" || n.type === "rangeSplineArea") && this._processMultiYPlotUnit(n)
	};
	t.prototype._processMultiseriesPlotUnit = function(n) {
		var s, v, a, o;
		if (n.dataSeriesIndexes && !(n.dataSeriesIndexes.length < 1)) {
			var e = n.axisY.dataInfo,
				u = n.axisX.dataInfo,
				r, f, h = !1;
			for (s = 0; s < n.dataSeriesIndexes.length; s++) {
				var i = this.data[n.dataSeriesIndexes[s]],
					t = 0,
					c = !1,
					l = !1;
				for ((i.axisPlacement === "normal" || i.axisPlacement === "xySwapped") && (v = this.sessionVariables.axisX.internalMinimum ? this.sessionVariables.axisX.internalMinimum : this._options.axisX && this._options.axisX.minimum ? this._options.axisX.minimum : -Infinity, a = this.sessionVariables.axisX.internalMaximum ? this.sessionVariables.axisX.internalMaximum : this._options.axisX && this._options.axisX.maximum ? this._options.axisX.maximum : Infinity), (i.dataPoints[t].x && i.dataPoints[t].x.getTime || i.xValueType === "dateTime") && (h = !0), t = 0; t < i.dataPoints.length; t++) {
					if (typeof i.dataPoints[t].x == "undefined" && (i.dataPoints[t].x = t), i.dataPoints[t].x.getTime ? (h = !0, r = i.dataPoints[t].x.getTime()) : r = i.dataPoints[t].x, f = i.dataPoints[t].y, r < u.min && (u.min = r), r > u.max && (u.max = r), f < e.min && (e.min = f), f > e.max && (e.max = f), t > 0 && (o = r - i.dataPoints[t - 1].x, o < 0 && (o = o * -1), u.minDiff > o && o !== 0 && (u.minDiff = o)), r < v && !c) continue;
					else if (!c && (c = !0, t > 0)) {
						t -= 2;
						continue
					}
					if (r > a && !l) l = !0;
					else if (r > a && l) continue;
					(i.dataPoints[t].label && (n.axisX.labels[r] = i.dataPoints[t].label), r < u.viewPortMin && (u.viewPortMin = r), r > u.viewPortMax && (u.viewPortMax = r), f !== null) && (f < e.viewPortMin && (e.viewPortMin = f), f > e.viewPortMax && (e.viewPortMax = f))
				}
				this.plotInfo.axisXValueType = i.xValueType = h ? "dateTime" : "number"
			}
		}
	};
	t.prototype._processStackedPlotUnit = function(n) {
		var l, w, p, s, r;
		if (n.dataSeriesIndexes && !(n.dataSeriesIndexes.length < 1)) {
			var u = n.axisY.dataInfo,
				e = n.axisX.dataInfo,
				i, o, a = !1,
				h = [],
				c = [];
			for (l = 0; l < n.dataSeriesIndexes.length; l++) {
				var f = this.data[n.dataSeriesIndexes[l]],
					t = 0,
					v = !1,
					y = !1;
				for ((f.axisPlacement === "normal" || f.axisPlacement === "xySwapped") && (w = this.sessionVariables.axisX.internalMinimum ? this.sessionVariables.axisX.internalMinimum : this._options.axisX && this._options.axisX.minimum ? this._options.axisX.minimum : -Infinity, p = this.sessionVariables.axisX.internalMaximum ? this.sessionVariables.axisX.internalMaximum : this._options.axisX && this._options.axisX.maximum ? this._options.axisX.maximum : Infinity), (f.dataPoints[t].x && f.dataPoints[t].x.getTime || f.xValueType === "dateTime") && (a = !0), t = 0; t < f.dataPoints.length; t++) {
					if (typeof f.dataPoints[t].x == "undefined" && (f.dataPoints[t].x = t), f.dataPoints[t].x.getTime ? (a = !0, i = f.dataPoints[t].x.getTime()) : i = f.dataPoints[t].x, o = f.dataPoints[t].y, i < e.min && (e.min = i), i > e.max && (e.max = i), t > 0 && (s = i - f.dataPoints[t - 1].x, s < 0 && (s = s * -1), e.minDiff > s && s !== 0 && (e.minDiff = s)), i < w && !v) continue;
					else if (!v && (v = !0, t > 0)) {
						t -= 2;
						continue
					}
					if (i > p && !y) y = !0;
					else if (i > p && y) continue;
					(f.dataPoints[t].label && (n.axisX.labels[i] = f.dataPoints[t].label), i < e.viewPortMin && (e.viewPortMin = i), i > e.viewPortMax && (e.viewPortMax = i), o !== null) && (n.yTotals[i] = (n.yTotals[i] ? n.yTotals[i] : 0) + Math.abs(o), o >= 0 ? h[i] ? h[i] += o : h[i] = o : c[i] ? c[i] += o : c[i] = o)
				}
				this.plotInfo.axisXValueType = f.xValueType = a ? "dateTime" : "number"
			}
			for (t in h) isNaN(t) || (r = h[t], r < u.min && (u.min = r), r > u.max && (u.max = r), t < e.viewPortMin || t > e.viewPortMax) || (r < u.viewPortMin && (u.viewPortMin = r), r > u.viewPortMax && (u.viewPortMax = r));
			for (t in c) isNaN(t) || (r = c[t], r < u.min && (u.min = r), r > u.max && (u.max = r), t < e.viewPortMin || t > e.viewPortMax) || (r < u.viewPortMin && (u.viewPortMin = r), r > u.viewPortMax && (u.viewPortMax = r))
		}
	};
	t.prototype._processStacked100PlotUnit = function(n) {
		var l, w, p, e;
		if (n.dataSeriesIndexes && !(n.dataSeriesIndexes.length < 1)) {
			var u = n.axisY.dataInfo,
				f = n.axisX.dataInfo,
				t, o, a = !1,
				s = !1,
				h = !1,
				c = [];
			for (l = 0; l < n.dataSeriesIndexes.length; l++) {
				var r = this.data[n.dataSeriesIndexes[l]],
					i = 0,
					v = !1,
					y = !1;
				for ((r.axisPlacement === "normal" || r.axisPlacement === "xySwapped") && (w = this.sessionVariables.axisX.internalMinimum ? this.sessionVariables.axisX.internalMinimum : this._options.axisX && this._options.axisX.minimum ? this._options.axisX.minimum : -Infinity, p = this.sessionVariables.axisX.internalMaximum ? this.sessionVariables.axisX.internalMaximum : this._options.axisX && this._options.axisX.maximum ? this._options.axisX.maximum : Infinity), (r.dataPoints[i].x && r.dataPoints[i].x.getTime || r.xValueType === "dateTime") && (a = !0), i = 0; i < r.dataPoints.length; i++) {
					if (typeof r.dataPoints[i].x == "undefined" && (r.dataPoints[i].x = i), r.dataPoints[i].x.getTime ? (a = !0, t = r.dataPoints[i].x.getTime()) : t = r.dataPoints[i].x, o = r.dataPoints[i].y, t < f.min && (f.min = t), t > f.max && (f.max = t), i > 0 && (e = t - r.dataPoints[i - 1].x, e < 0 && (e = e * -1), f.minDiff > e && e !== 0 && (f.minDiff = e)), t < w && !v) continue;
					else if (!v && (v = !0, i > 0)) {
						i -= 2;
						continue
					}
					if (t > p && !y) y = !0;
					else if (t > p && y) continue;
					(r.dataPoints[i].label && (n.axisX.labels[t] = r.dataPoints[i].label), t < f.viewPortMin && (f.viewPortMin = t), t > f.viewPortMax && (f.viewPortMax = t), o !== null) && (n.yTotals[t] = (n.yTotals[t] ? n.yTotals[t] : 0) + Math.abs(o), o >= 0 ? s = !0 : h = !0, c[t] ? c[t] += Math.abs(o) : c[t] = Math.abs(o))
				}
				this.plotInfo.axisXValueType = r.xValueType = a ? "dateTime" : "number"
			}
			s && !h ? (u.max = 99, u.min = 1) : s && h ? (u.max = 99, u.min = -99) : !s && h && (u.max = -1, u.min = -99);
			u.viewPortMin = u.min;
			u.viewPortMax = u.max;
			n.dataPointYSums = c
		}
	};
	t.prototype._processMultiYPlotUnit = function(n) {
		var c, p, y, e;
		if (n.dataSeriesIndexes && !(n.dataSeriesIndexes.length < 1)) {
			var f = n.axisY.dataInfo,
				u = n.axisX.dataInfo,
				r, o, s, h, l = !1;
			for (c = 0; c < n.dataSeriesIndexes.length; c++) {
				var i = this.data[n.dataSeriesIndexes[c]],
					t = 0,
					a = !1,
					v = !1;
				for ((i.axisPlacement === "normal" || i.axisPlacement === "xySwapped") && (p = this.sessionVariables.axisX.internalMinimum ? this.sessionVariables.axisX.internalMinimum : this._options.axisX && this._options.axisX.minimum ? this._options.axisX.minimum : -Infinity, y = this.sessionVariables.axisX.internalMaximum ? this.sessionVariables.axisX.internalMaximum : this._options.axisX && this._options.axisX.maximum ? this._options.axisX.maximum : Infinity), (i.dataPoints[t].x && i.dataPoints[t].x.getTime || i.xValueType === "dateTime") && (l = !0), t = 0; t < i.dataPoints.length; t++) {
					if (typeof i.dataPoints[t].x == "undefined" && (i.dataPoints[t].x = t), i.dataPoints[t].x.getTime ? (l = !0, r = i.dataPoints[t].x.getTime()) : r = i.dataPoints[t].x, o = i.dataPoints[t].y, o && o.length && (s = Math.min.apply(null, o), h = Math.max.apply(null, o)), r < u.min && (u.min = r), r > u.max && (u.max = r), s < f.min && (f.min = s), h > f.max && (f.max = h), t > 0 && (e = r - i.dataPoints[t - 1].x, e < 0 && (e = e * -1), u.minDiff > e && e !== 0 && (u.minDiff = e)), r < p && !a) continue;
					else if (!a && (a = !0, t > 0)) {
						t -= 2;
						continue
					}
					if (r > y && !v) v = !0;
					else if (r > y && v) continue;
					(i.dataPoints[t].label && (n.axisX.labels[r] = i.dataPoints[t].label), r < u.viewPortMin && (u.viewPortMin = r), r > u.viewPortMax && (u.viewPortMax = r), o !== null) && (s < f.viewPortMin && (f.viewPortMin = s), h > f.viewPortMax && (f.viewPortMax = h))
				}
				this.plotInfo.axisXValueType = i.xValueType = l ? "dateTime" : "number"
			}
		}
	};
	t.prototype.getDataPointAtXY = function(n, t, i) {
		var u, e, h, o, f, s, r, c;
		for (i = i || !1, u = [], e = this._dataInRenderedOrder.length - 1; e >= 0; e--) h = this._dataInRenderedOrder[e], o = null, o = h.getDataPointAtXY(n, t, i), o && u.push(o);
		for (f = null, s = !1, r = 0; r < u.length; r++)
			if ((u[r].dataSeries.type === "line" || u[r].dataSeries.type === "stepLine" || u[r].dataSeries.type === "area" || u[r].dataSeries.type === "stepArea") && (c = p("markerSize", u[r].dataPoint, u[r].dataSeries) || 8, u[r].distance <= c / 2)) {
				s = !0;
				break
			} for (r = 0; r < u.length; r++) s && u[r].dataSeries.type !== "line" && u[r].dataSeries.type !== "stepLine" && u[r].dataSeries.type !== "area" && u[r].dataSeries.type !== "stepArea" || (f ? u[r].distance <= f.distance && (f = u[r]) : f = u[r]);
		return f
	};
	t.prototype.getObjectAtXY = function(t, i, r) {
		var f, e, o, u;
		if (r = r || !1, f = null, e = this.getDataPointAtXY(t, i, r), e) f = e.dataSeries.dataPointIds[e.dataPointIndex];
		else if (n) f = ci(t, i, this._eventManager.ghostCtx);
		else
			for (o = 0; o < this.legend.items.length; o++) u = this.legend.items[o], t >= u.x1 && t <= u.x2 && i >= u.y1 && i <= u.y2 && (f = u.id);
		return f
	};
	t.prototype.getAutoFontSize = function(n, t, i) {
		t = t || this.width;
		i = i || this.height;
		var r = n / 400;
		return Math.round(Math.min(this.width, this.height) * r)
	};
	t.prototype.resetOverlayedCanvas = function() {
		this.overlaidCanvasCtx.clearRect(0, 0, this.width, this.height)
	};
	t.prototype.clearCanvas = function() {
		this.ctx.clearRect(0, 0, this.width, this.height);
		this.backgroundColor && (this.ctx.fillStyle = this.backgroundColor, this.ctx.fillRect(0, 0, this.width, this.height))
	};
	t.prototype.attachEvent = function(n) {
		this._events.push(n)
	};
	t.prototype._touchEventHandler = function(n) {
		var f, e, h, o;
		if (n.changedTouches && this.interactivityEnabled) {
			var i = [],
				u = n.changedTouches,
				t = u ? u[0] : n,
				r = null;
			switch (n.type) {
				case "touchstart":
				case "MSPointerDown":
					i = ["mousemove", "mousedown"];
					this._lastTouchData = wt(t);
					this._lastTouchData.time = new Date;
					break;
				case "touchmove":
				case "MSPointerMove":
					i = ["mousemove"];
					break;
				case "touchend":
				case "MSPointerUp":
					i = this._lastTouchEventType === "touchstart" || this._lastTouchEventType === "MSPointerDown" ? ["mouseup", "click"] : ["mouseup"];
					break;
				default:
					return
			}
			if (!u || !(u.length > 1)) {
				r = wt(t);
				r.time = new Date;
				try {
					var s = r.y - this._lastTouchData.y,
						l = r.x - this._lastTouchData.x,
						c = r.time - this._lastTouchData.time;
					Math.abs(s) > 15 && (!!this._lastTouchData.scroll || c < 200) && (this._lastTouchData.scroll = !0, f = window.parent || window, f && f.scrollBy && f.scrollBy(0, -s))
				} catch (a) {}
				if (this._lastTouchEventType = n.type, !!this._lastTouchData.scroll && this.zoomEnabled) {
					this.isDrag && this.resetOverlayedCanvas();
					this.isDrag = !1;
					return
				}
				for (e = 0; e < i.length; e++) h = i[e], o = document.createEvent("MouseEvent"), o.initMouseEvent(h, !0, !0, window, 1, t.screenX, t.screenY, t.clientX, t.clientY, !1, !1, !1, !1, 0, null), t.target.dispatchEvent(o), n.preventManipulation && n.preventManipulation(), n.preventDefault && n.preventDefault()
			}
		}
	};
	t.prototype._mouseEventHandler = function(n) {
		var r, u, i, s, h, f, e, o;
		if (this.interactivityEnabled) {
			if (this._ignoreNextEvent) {
				this._ignoreNextEvent = !1;
				return
			}
			if (n.preventManipulation && n.preventManipulation(), n.preventDefault && n.preventDefault(), typeof n.target == "undefined" && n.srcElement && (n.target = n.srcElement), r = wt(n), u = n.type, n || (h = window.event), n.which ? s = n.which == 3 : n.button && (s = n.button == 2), yt && window.console && (window.console.log(u + " --> x: " + r.x + "; y:" + r.y), s && window.console.log(n.which), u === "mouseup" && window.console.log("mouseup")), !s) {
				if (t.capturedEventParam) i = t.capturedEventParam, u === "mouseup" && (t.capturedEventParam = null, i.chart.overlaidCanvas.releaseCapture ? i.chart.overlaidCanvas.releaseCapture() : document.body.removeEventListener("mouseup", i.chart._mouseEventHandler, !1)), i.hasOwnProperty(u) && i[u].call(i.context, r.x, r.y);
				else if (this._events) {
					for (f = 0; f < this._events.length; f++)
						if (this._events[f].hasOwnProperty(u))
							if (i = this._events[f], e = i.bounds, r.x >= e.x1 && r.x <= e.x2 && r.y >= e.y1 && r.y <= e.y2) {
								i[u].call(i.context, r.x, r.y);
								u === "mousedown" && i.capture === !0 ? (t.capturedEventParam = i, this.overlaidCanvas.setCapture ? this.overlaidCanvas.setCapture() : document.body.addEventListener("mouseup", this._mouseEventHandler, !1)) : u === "mouseup" && (i.chart.overlaidCanvas.releaseCapture ? i.chart.overlaidCanvas.releaseCapture() : document.body.removeEventListener("mouseup", this._mouseEventHandler, !1));
								break
							} else i = null;
					n.target.style.cursor = i && i.cursor ? i.cursor : this._defaultCursor
				}
				this._toolTip && this._toolTip.enabled && (o = this.plotArea, (r.x < o.x1 || r.x > o.x2 || r.y < o.y1 || r.y > o.y2) && this._toolTip.hide());
				this.isDrag && this.zoomEnabled || !this._eventManager || this._eventManager.mouseEventHandler(n)
			}
		}
	};
	t.prototype._plotAreaMouseDown = function(n, t) {
		this.isDrag = !0;
		this.dragStartPoint = this.plotInfo.axisPlacement !== "none" ? {
			x: n,
			y: t,
			xMinimum: this.axisX.minimum,
			xMaximum: this.axisX.maximum
		} : {
			x: n,
			y: t
		}
	};
	t.prototype._plotAreaMouseUp = function(n, t) {
		var s, e, r;
		if ((this.plotInfo.axisPlacement === "normal" || this.plotInfo.axisPlacement === "xySwapped") && this.isDrag) {
			var o = 0,
				h = 0,
				i = this.axisX.lineCoordinates;
			if (this.plotInfo.axisPlacement === "xySwapped" ? (o = t - this.dragStartPoint.y, h = Math.abs(this.axisX.maximum - this.axisX.minimum) / i.height * o) : (o = this.dragStartPoint.x - n, h = Math.abs(this.axisX.maximum - this.axisX.minimum) / i.width * o), Math.abs(o) > 2) {
				if (this.panEnabled) s = !1, e = 0, this.axisX.sessionVariables.internalMinimum < this.axisX._absoluteMinimum ? (e = this.axisX._absoluteMinimum - this.axisX.sessionVariables.internalMinimum, this.axisX.sessionVariables.internalMinimum += e, this.axisX.sessionVariables.internalMaximum += e, s = !0) : this.axisX.sessionVariables.internalMaximum > this.axisX._absoluteMaximum && (e = this.axisX.sessionVariables.internalMaximum - this.axisX._absoluteMaximum, this.axisX.sessionVariables.internalMaximum -= e, this.axisX.sessionVariables.internalMinimum -= e, s = !0), s && this.render();
				else if (this.zoomEnabled) {
					if (this.resetOverlayedCanvas(), !this.dragStartPoint) return;
					if (this.plotInfo.axisPlacement === "xySwapped") {
						if (r = {
								y1: Math.min(this.dragStartPoint.y, t),
								y2: Math.max(this.dragStartPoint.y, t)
							}, Math.abs(r.y1 - r.y2) > 1) {
							var i = this.axisX.lineCoordinates,
								u = this.axisX.maximum - (this.axisX.maximum - this.axisX.minimum) / i.height * (r.y2 - i.y1),
								f = this.axisX.maximum - (this.axisX.maximum - this.axisX.minimum) / i.height * (r.y1 - i.y1);
							u = Math.max(u, this.axisX.dataInfo.min);
							f = Math.min(f, this.axisX.dataInfo.max);
							Math.abs(f - u) > 2 * Math.abs(this.axisX.dataInfo.minDiff) && (this.axisX.sessionVariables.internalMinimum = u, this.axisX.sessionVariables.internalMaximum = f, this.render())
						}
					} else if (this.plotInfo.axisPlacement === "normal" && (r = {
							x1: Math.min(this.dragStartPoint.x, n),
							x2: Math.max(this.dragStartPoint.x, n)
						}, Math.abs(r.x1 - r.x2) > 1)) {
						var i = this.axisX.lineCoordinates,
							u = (this.axisX.maximum - this.axisX.minimum) / i.width * (r.x1 - i.x1) + this.axisX.minimum,
							f = (this.axisX.maximum - this.axisX.minimum) / i.width * (r.x2 - i.x1) + this.axisX.minimum;
						u = Math.max(u, this.axisX.dataInfo.min);
						f = Math.min(f, this.axisX.dataInfo.max);
						Math.abs(f - u) > 2 * Math.abs(this.axisX.dataInfo.minDiff) && (this.axisX.sessionVariables.internalMinimum = u, this.axisX.sessionVariables.internalMaximum = f, this.render())
					}
				}
				this._ignoreNextEvent = !0;
				this.zoomEnabled && this._zoomButton.style.display === "none" && (ui(this._zoomButton, this._resetButton), b(this, this._zoomButton, "pan"), b(this, this._resetButton, "reset"))
			}
		}
		this.isDrag = !1
	};
	t.prototype._plotAreaMouseMove = function(n, t) {
		var r, o, u, s;
		if (this.isDrag && this.plotInfo.axisPlacement !== "none") {
			var i = 0,
				f = 0,
				e = this.axisX.lineCoordinates;
			this.plotInfo.axisPlacement === "xySwapped" ? (i = t - this.dragStartPoint.y, f = Math.abs(this.axisX.maximum - this.axisX.minimum) / e.height * i) : (i = this.dragStartPoint.x - n, f = Math.abs(this.axisX.maximum - this.axisX.minimum) / e.width * i);
			Math.abs(i) > 2 && Math.abs(i) < 8 && (this.panEnabled || this.zoomEnabled) ? this._toolTip.hide() : this.panEnabled || this.zoomEnabled || this._toolTip.mouseMoveHandler(n, t);
			Math.abs(i) > 2 && (this.panEnabled || this.zoomEnabled) && (this.panEnabled ? (this.axisX.sessionVariables.internalMinimum = this.dragStartPoint.xMinimum + f, this.axisX.sessionVariables.internalMaximum = this.dragStartPoint.xMaximum + f, r = 0, this.axisX.sessionVariables.internalMinimum < this.axisX._absoluteMinimum - st(this.axisX.interval, this.axisX.intervalType) ? (r = this.axisX._absoluteMinimum - st(this.axisX.interval, this.axisX.intervalType) - this.axisX.sessionVariables.internalMinimum, this.axisX.sessionVariables.internalMinimum += r, this.axisX.sessionVariables.internalMaximum += r) : this.axisX.sessionVariables.internalMaximum > this.axisX._absoluteMaximum + st(this.axisX.interval, this.axisX.intervalType) && (r = this.axisX.sessionVariables.internalMaximum - (this.axisX._absoluteMaximum + st(this.axisX.interval, this.axisX.intervalType)), this.axisX.sessionVariables.internalMaximum -= r, this.axisX.sessionVariables.internalMinimum -= r), o = this, clearTimeout(this._panTimerId), this._panTimerId = setTimeout(function() {
				o.render()
			}, 0)) : this.zoomEnabled && (u = this.plotArea, this.resetOverlayedCanvas(), s = this.overlaidCanvasCtx.globalAlpha, this.overlaidCanvasCtx.globalAlpha = .7, this.overlaidCanvasCtx.fillStyle = "#A0ABB8", this.plotInfo.axisPlacement === "xySwapped" ? this.overlaidCanvasCtx.fillRect(u.x1, this.dragStartPoint.y, u.x2 - u.x1, t - this.dragStartPoint.y) : this.plotInfo.axisPlacement === "normal" && this.overlaidCanvasCtx.fillRect(this.dragStartPoint.x, u.y1, n - this.dragStartPoint.x, u.y2 - u.y1), this.overlaidCanvasCtx.globalAlpha = s))
		} else this._toolTip.mouseMoveHandler(n, t)
	};
	t.prototype.preparePlotArea = function() {
		var t = this.plotArea,
			i = this.axisY ? this.axisY : this.axisY2,
			r;
		!n && (t.x1 > 0 || t.y1 > 0) && t.ctx.translate(t.x1, t.y1);
		this.axisX && i ? (t.x1 = this.axisX.lineCoordinates.x1 < this.axisX.lineCoordinates.x2 ? this.axisX.lineCoordinates.x1 : i.lineCoordinates.x1, t.y1 = this.axisX.lineCoordinates.y1 < i.lineCoordinates.y1 ? this.axisX.lineCoordinates.y1 : i.lineCoordinates.y1, t.x2 = this.axisX.lineCoordinates.x2 > i.lineCoordinates.x2 ? this.axisX.lineCoordinates.x2 : i.lineCoordinates.x2, t.y2 = this.axisX.lineCoordinates.y2 > this.axisX.lineCoordinates.y1 ? this.axisX.lineCoordinates.y2 : i.lineCoordinates.y2, t.width = t.x2 - t.x1, t.height = t.y2 - t.y1) : (r = this.layoutManager.getFreeSpace(), t.x1 = r.x1, t.x2 = r.x2, t.y1 = r.y1, t.y2 = r.y2, t.width = r.width, t.height = r.height);
		n || (t.canvas.width = t.width, t.canvas.height = t.height, t.canvas.style.left = t.x1 + "px", t.canvas.style.top = t.y1 + "px", (t.x1 > 0 || t.y1 > 0) && t.ctx.translate(-t.x1, -t.y1));
		t.layoutManager = new ft(t.x1, t.y1, t.x2, t.y2, 2)
	};
	t.prototype.getPixelCoordinatesOnPlotArea = function(n, t) {
		return {
			x: this.axisX.getPixelCoordinatesOnAxis(n).x,
			y: this.axisY.getPixelCoordinatesOnAxis(t).y
		}
	};
	t.prototype.renderIndexLabels = function(n) {
		for (var nt, ot = n || this.plotArea.ctx, u = this.plotArea, y = 0, w = 0, b = 0, d = 0, g = 0, h = 0, f = 0, l = 0, e = 0, it = 0; it < this._indexLabels.length; it++) {
			var t = this._indexLabels[it],
				i = t.chartType.toLowerCase(),
				a, o, wt, ct = p("indexLabelFontColor", t.dataPoint, t.dataSeries),
				rt = p("indexLabelFontSize", t.dataPoint, t.dataSeries),
				lt = p("indexLabelFontFamily", t.dataPoint, t.dataSeries),
				at = p("indexLabelFontStyle", t.dataPoint, t.dataSeries),
				vt = p("indexLabelFontWeight", t.dataPoint, t.dataSeries),
				yt = p("indexLabelBackgroundColor", t.dataPoint, t.dataSeries),
				st = p("indexLabelMaxWidth", t.dataPoint, t.dataSeries),
				pt = p("indexLabelWrap", t.dataPoint, t.dataSeries),
				ut = {
					percent: null,
					total: null
				},
				ft = null;
			if ((t.dataSeries.type.indexOf("stacked") >= 0 || t.dataSeries.type === "pie" || t.dataSeries.type === "doughnut") && (ut = this.getPercentAndTotal(t.dataSeries, t.dataPoint)), (t.dataSeries.indexLabelFormatter || t.dataPoint.indexLabelFormatter) && (ft = {
					chart: this._options,
					dataSeries: t.dataSeries,
					dataPoint: t.dataPoint,
					index: t.indexKeyword,
					total: ut.total,
					percent: ut.percent
				}), nt = t.dataPoint.indexLabelFormatter ? t.dataPoint.indexLabelFormatter(ft) : t.dataPoint.indexLabel ? this.replaceKeywordsWithValue(t.dataPoint.indexLabel, t.dataPoint, t.dataSeries, null, t.indexKeyword) : t.dataSeries.indexLabelFormatter ? t.dataSeries.indexLabelFormatter(ft) : t.dataSeries.indexLabel ? this.replaceKeywordsWithValue(t.dataSeries.indexLabel, t.dataPoint, t.dataSeries, null, t.indexKeyword) : null, nt !== null && nt !== "") {
				var s = p("indexLabelPlacement", t.dataPoint, t.dataSeries),
					et = p("indexLabelOrientation", t.dataPoint, t.dataSeries),
					wt = 0,
					k = t.direction,
					tt = t.dataSeries.axisX,
					ht = t.dataSeries.axisY,
					v = new c(ot, {
						x: 0,
						y: 0,
						maxWidth: st ? st : this.width * .5,
						maxHeight: pt ? rt * 5 : rt * 1.5,
						angle: et === "horizontal" ? 0 : -90,
						text: nt,
						padding: 0,
						backgroundColor: yt,
						horizontalAlign: "left",
						fontSize: rt,
						fontFamily: lt,
						fontWeight: vt,
						fontColor: ct,
						fontStyle: at,
						textBaseline: "top"
					}),
					bt = v.measureText();
				if (i.indexOf("line") >= 0 || i.indexOf("area") >= 0 || i.indexOf("bubble") >= 0 || i.indexOf("scatter") >= 0) {
					if (t.dataPoint.x < tt.minimum || t.dataPoint.x > tt.maximum || t.dataPoint.y < ht.minimum || t.dataPoint.y > ht.maximum) continue
				} else if (t.dataPoint.x < tt.minimum || t.dataPoint.x > tt.maximum) continue;
				f = 2;
				h = 2;
				et === "horizontal" ? (l = v.width, e = v.height) : (e = v.width, l = v.height);
				this.plotInfo.axisPlacement === "normal" ? (i.indexOf("line") >= 0 || i.indexOf("area") >= 0 ? (s = "auto", f = 4) : i.indexOf("stacked") >= 0 ? s === "auto" && (s = "inside") : (i === "bubble" || i === "scatter") && (s = "inside"), a = t.point.x - l / 2, s !== "inside" ? (w = u.y1, b = u.y2, k > 0 ? (o = t.point.y - e - f, o < w && (o = s === "auto" ? Math.max(t.point.y, w) + f : w + f)) : (o = t.point.y + f, o > b - e - f && (o = s === "auto" ? Math.min(t.point.y, b) - e - f : b - e - f))) : (w = Math.max(t.bounds.y1, u.y1), b = Math.min(t.bounds.y2, u.y2), y = i.indexOf("range") >= 0 ? k > 0 ? Math.max(t.bounds.y1, u.y1) + e / 2 + f : Math.min(t.bounds.y2, u.y2) - e / 2 - f : (Math.max(t.bounds.y1, u.y1) + Math.min(t.bounds.y2, u.y2)) / 2, k > 0 ? (o = Math.max(t.point.y, y) - e / 2, o < w && (i === "bubble" || i === "scatter") && (o = Math.max(t.point.y - e - f, u.y1 + f))) : (o = Math.min(t.point.y, y) - e / 2, o > b - e - f && (i === "bubble" || i === "scatter") && (o = Math.min(t.point.y + f, u.y2 - e - f))))) : (i.indexOf("line") >= 0 || i.indexOf("area") >= 0 || i.indexOf("scatter") >= 0 ? (s = "auto", h = 4) : i.indexOf("stacked") >= 0 ? s === "auto" && (s = "inside") : i === "bubble" && (s = "inside"), o = t.point.y - e / 2, s !== "inside" ? (d = u.x1, g = u.x2, k < 0 ? (a = t.point.x - l - h, a < d && (a = s === "auto" ? Math.max(t.point.x, d) + h : d + h)) : (a = t.point.x + h, a > g - l - h && (a = s === "auto" ? Math.min(t.point.x, g) - l - h : g - l - h))) : (d = Math.max(t.bounds.x1, u.x1), g = Math.min(t.bounds.x2, u.x2), y = i.indexOf("range") >= 0 ? k < 0 ? Math.max(t.bounds.x1, u.x1) + l / 2 + h : Math.min(t.bounds.x2, u.x2) - l / 2 - h : (Math.max(t.bounds.x1, u.x1) + Math.min(t.bounds.x2, u.x2)) / 2, a = k < 0 ? Math.max(t.point.x, y) - l / 2 : Math.min(t.point.x, y) - l / 2));
				et === "vertical" && (o += e);
				v.x = a;
				v.y = o;
				v.render(!0)
			}
		}
		return {
			source: ot,
			dest: this.plotArea.ctx,
			animationCallback: r.fadeInAnimation,
			easingFunction: r.easing.easeInQuad,
			animationBase: 0,
			startTimePercent: .7
		}
	};
	t.prototype.renderLine = function(t) {
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			et = t.dataSeriesIndexes.length,
			o, l, p, w, b, e, s, nt, ut, ft, tt, it, f, h, c, k, d, g, v, rt;
		if (!(et <= 0)) {
			for (o = this._eventManager.ghostCtx, i.save(), l = this.plotArea, i.beginPath(), i.rect(l.x1, l.y1, l.width, l.height), i.clip(), p = [], w = 0; w < t.dataSeriesIndexes.length; w++)
				if (b = t.dataSeriesIndexes[w], e = this.data[b], i.lineWidth = e.lineThickness, s = e.dataPoints, i.setLineDash && i.setLineDash(y(e.lineDashType, e.lineThickness)), nt = e.id, this._eventManager.objectMap[nt] = {
						objectType: "dataSeries",
						dataSeriesIndex: b
					}, ut = u(nt), o.strokeStyle = ut, o.lineWidth = e.lineThickness > 0 ? Math.max(e.lineThickness, 4) : 0, ft = e._colorSet, tt = ft[0], i.strokeStyle = tt, it = !0, f = 0, i.beginPath(), s.length > 0) {
					for (d = !1, f = 0; f < s.length; f++)
						if (k = s[f].x.getTime ? s[f].x.getTime() : s[f].x, !(k < t.axisX.dataInfo.viewPortMin) && !(k > t.axisX.dataInfo.viewPortMax)) {
							if (typeof s[f].y != "number") {
								f > 0 && (i.stroke(), n && o.stroke());
								d = !0;
								continue
							}
							h = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (k - t.axisX.conversionParameters.minimum) + .5 << 0;
							c = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (s[f].y - t.axisY.conversionParameters.minimum) + .5 << 0;
							g = e.dataPointIds[f];
							this._eventManager.objectMap[g] = {
								id: g,
								objectType: "dataPoint",
								dataSeriesIndex: b,
								dataPointIndex: f,
								x1: h,
								y1: c
							};
							it || d ? (i.beginPath(), i.moveTo(h, c), n && (o.beginPath(), o.moveTo(h, c)), it = !1, d = !1) : (i.lineTo(h, c), n && o.lineTo(h, c), f % 500 == 0 && (i.stroke(), i.beginPath(), i.moveTo(h, c), n && (o.stroke(), o.beginPath(), o.moveTo(h, c))));
							(s[f].markerSize > 0 || e.markerSize > 0) && (v = e.getMarkerProperties(f, h, c, i), p.push(v), rt = u(g), n && p.push({
								x: h,
								y: c,
								ctx: o,
								type: v.type,
								size: v.size,
								color: rt,
								borderColor: rt,
								borderThickness: v.borderThickness
							}));
							(s[f].indexLabel || e.indexLabel || s[f].indexLabelFormatter || e.indexLabelFormatter) && this._indexLabels.push({
								chartType: "line",
								dataPoint: s[f],
								dataSeries: e,
								point: {
									x: h,
									y: c
								},
								direction: s[f].y >= 0 ? 1 : -1,
								color: tt
							})
						} i.stroke();
					n && o.stroke()
				} return a.drawMarkers(p), i.restore(), i.beginPath(), n && o.beginPath(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderStepLine = function(t) {
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			ot = t.dataSeriesIndexes.length,
			o, l, p, w, b, e, s, nt, ft, et, tt, it, f, h, c, k, d, rt, g, v, ut;
		if (!(ot <= 0)) {
			for (o = this._eventManager.ghostCtx, i.save(), l = this.plotArea, i.beginPath(), i.rect(l.x1, l.y1, l.width, l.height), i.clip(), p = [], w = 0; w < t.dataSeriesIndexes.length; w++)
				if (b = t.dataSeriesIndexes[w], e = this.data[b], i.lineWidth = e.lineThickness, s = e.dataPoints, i.setLineDash && i.setLineDash(y(e.lineDashType, e.lineThickness)), nt = e.id, this._eventManager.objectMap[nt] = {
						objectType: "dataSeries",
						dataSeriesIndex: b
					}, ft = u(nt), o.strokeStyle = ft, o.lineWidth = e.lineThickness > 0 ? Math.max(e.lineThickness, 4) : 0, et = e._colorSet, tt = et[0], i.strokeStyle = tt, it = !0, f = 0, i.beginPath(), s.length > 0) {
					for (d = !1, f = 0; f < s.length; f++)
						if (k = s[f].getTime ? s[f].x.getTime() : s[f].x, !(k < t.axisX.dataInfo.viewPortMin) && !(k > t.axisX.dataInfo.viewPortMax)) {
							if (typeof s[f].y != "number") {
								f > 0 && (i.stroke(), n && o.stroke());
								d = !0;
								continue
							}
							rt = c;
							h = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (k - t.axisX.conversionParameters.minimum) + .5 << 0;
							c = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (s[f].y - t.axisY.conversionParameters.minimum) + .5 << 0;
							g = e.dataPointIds[f];
							this._eventManager.objectMap[g] = {
								id: g,
								objectType: "dataPoint",
								dataSeriesIndex: b,
								dataPointIndex: f,
								x1: h,
								y1: c
							};
							it || d ? (i.beginPath(), i.moveTo(h, c), n && (o.beginPath(), o.moveTo(h, c)), it = !1, d = !1) : (i.lineTo(h, rt), n && o.lineTo(h, rt), i.lineTo(h, c), n && o.lineTo(h, c), f % 500 == 0 && (i.stroke(), i.beginPath(), i.moveTo(h, c), n && (o.stroke(), o.beginPath(), o.moveTo(h, c))));
							(s[f].markerSize > 0 || e.markerSize > 0) && (v = e.getMarkerProperties(f, h, c, i), p.push(v), ut = u(g), n && p.push({
								x: h,
								y: c,
								ctx: o,
								type: v.type,
								size: v.size,
								color: ut,
								borderColor: ut,
								borderThickness: v.borderThickness
							}));
							(s[f].indexLabel || e.indexLabel || s[f].indexLabelFormatter || e.indexLabelFormatter) && this._indexLabels.push({
								chartType: "stepLine",
								dataPoint: s[f],
								dataSeries: e,
								point: {
									x: h,
									y: c
								},
								direction: s[f].y >= 0 ? 1 : -1,
								color: tt
							})
						} i.stroke();
					n && o.stroke()
				} return a.drawMarkers(p), i.restore(), i.beginPath(), n && o.beginPath(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderSpline = function(t) {
		function ft(t) {
			var r = kt(t, 2),
				u;
			if (r.length > 0) {
				for (i.beginPath(), n && s.beginPath(), i.moveTo(r[0].x, r[0].y), n && s.moveTo(r[0].x, r[0].y), u = 0; u < r.length - 3; u += 3) i.bezierCurveTo(r[u + 1].x, r[u + 1].y, r[u + 2].x, r[u + 2].y, r[u + 3].x, r[u + 3].y), n && s.bezierCurveTo(r[u + 1].x, r[u + 1].y, r[u + 2].x, r[u + 2].y, r[u + 3].x, r[u + 3].y), u > 0 && u % 3e3 == 0 && (i.stroke(), i.beginPath(), i.moveTo(r[u + 3].x, r[u + 3].y), n && (s.stroke(), s.beginPath(), s.moveTo(r[u + 3].x, r[u + 3].y)));
				i.stroke();
				n && s.stroke()
			}
		}
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			et = t.dataSeriesIndexes.length,
			s, l, w, b, k, e, o, nt, rt, ut, tt, g, p, it;
		if (!(et <= 0)) {
			for (s = this._eventManager.ghostCtx, i.save(), l = this.plotArea, i.beginPath(), i.rect(l.x1, l.y1, l.width, l.height), i.clip(), w = [], b = 0; b < t.dataSeriesIndexes.length; b++) {
				k = t.dataSeriesIndexes[b];
				e = this.data[k];
				i.lineWidth = e.lineThickness;
				o = e.dataPoints;
				i.setLineDash && i.setLineDash(y(e.lineDashType, e.lineThickness));
				nt = e.id;
				this._eventManager.objectMap[nt] = {
					objectType: "dataSeries",
					dataSeriesIndex: k
				};
				rt = u(nt);
				s.strokeStyle = rt;
				s.lineWidth = e.lineThickness > 0 ? Math.max(e.lineThickness, 4) : 0;
				ut = e._colorSet;
				tt = ut[0];
				i.strokeStyle = tt;
				var f = 0,
					h, c, d, v = [];
				if (i.beginPath(), o.length > 0)
					for (f = 0; f < o.length; f++)
						if (d = o[f].getTime ? o[f].x.getTime() : o[f].x, !(d < t.axisX.dataInfo.viewPortMin) && !(d > t.axisX.dataInfo.viewPortMax)) {
							if (typeof o[f].y != "number") {
								f > 0 && (ft(v), v = []);
								continue
							}
							h = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (d - t.axisX.conversionParameters.minimum) + .5 << 0;
							c = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (o[f].y - t.axisY.conversionParameters.minimum) + .5 << 0;
							g = e.dataPointIds[f];
							this._eventManager.objectMap[g] = {
								id: g,
								objectType: "dataPoint",
								dataSeriesIndex: k,
								dataPointIndex: f,
								x1: h,
								y1: c
							};
							v[v.length] = {
								x: h,
								y: c
							};
							(o[f].markerSize > 0 || e.markerSize > 0) && (p = e.getMarkerProperties(f, h, c, i), w.push(p), it = u(g), n && w.push({
								x: h,
								y: c,
								ctx: s,
								type: p.type,
								size: p.size,
								color: it,
								borderColor: it,
								borderThickness: p.borderThickness
							}));
							(o[f].indexLabel || e.indexLabel || o[f].indexLabelFormatter || e.indexLabelFormatter) && this._indexLabels.push({
								chartType: "spline",
								dataPoint: o[f],
								dataSeries: e,
								point: {
									x: h,
									y: c
								},
								direction: o[f].y >= 0 ? 1 : -1,
								color: tt
							})
						} ft(v)
			}
			return a.drawMarkers(w), i.restore(), i.beginPath(), n && s.beginPath(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	o = function(n, t, i, r, u, f, e, o, s, h, c, l, a) {
		var v, w, y;
		typeof a == "undefined" && (a = 1);
		e = e || 0;
		o = o || "black";
		var k = t,
			d = r,
			g = i,
			nt = u;
		v = r - t > 15 && u - i > 15 ? 8 : .35 * Math.min(r - t, u - i);
		var tt = "rgba(255, 255, 255, .4)",
			b = "rgba(255, 255, 255, 0.1)",
			p = f;
		n.beginPath();
		n.moveTo(t, i);
		n.save();
		n.fillStyle = p;
		n.globalAlpha = a;
		n.fillRect(t, i, r - t, u - i);
		n.globalAlpha = 1;
		e > 0 && (w = e % 2 == 0 ? 0 : .5, n.beginPath(), n.lineWidth = e, n.strokeStyle = o, n.moveTo(t, i), n.rect(t - w, i - w, r - t + 2 * w, u - i + 2 * w), n.stroke());
		n.restore();
		s === !0 && (n.save(), n.beginPath(), n.moveTo(t, i), n.lineTo(t + v, i + v), n.lineTo(r - v, i + v), n.lineTo(r, i), n.closePath(), y = n.createLinearGradient((r + t) / 2, g + v, (r + t) / 2, g), y.addColorStop(0, p), y.addColorStop(1, tt), n.fillStyle = y, n.fill(), n.restore());
		h === !0 && (n.save(), n.beginPath(), n.moveTo(t, u), n.lineTo(t + v, u - v), n.lineTo(r - v, u - v), n.lineTo(r, u), n.closePath(), y = n.createLinearGradient((r + t) / 2, nt - v, (r + t) / 2, nt), y.addColorStop(0, p), y.addColorStop(1, tt), n.fillStyle = y, n.fill(), n.restore());
		c === !0 && (n.save(), n.beginPath(), n.moveTo(t, i), n.lineTo(t + v, i + v), n.lineTo(t + v, u - v), n.lineTo(t, u), n.closePath(), y = n.createLinearGradient(k + v, (u + i) / 2, k, (u + i) / 2), y.addColorStop(0, p), y.addColorStop(1, b), n.fillStyle = y, n.fill(), n.restore());
		l === !0 && (n.save(), n.beginPath(), n.moveTo(r, i), n.lineTo(r - v, i + v), n.lineTo(r - v, u - v), n.lineTo(r, u), y = n.createLinearGradient(d - v, (u + i) / 2, d, (u + i) / 2), y.addColorStop(0, p), y.addColorStop(1, b), n.fillStyle = y, y.addColorStop(0, p), y.addColorStop(1, b), n.fillStyle = y, n.fill(), n.closePath(), n.restore())
	};
	t.prototype.renderColumn = function(t) {
		var v = t.targetCanvasCtx || this.plotArea.ctx,
			st = t.dataSeriesIndexes.length,
			w, tt, a, y, e, s, ft, k, et, ot;
		if (!(st <= 0)) {
			var p = null,
				h = this.plotArea,
				i = 0,
				it, d, b, g = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				nt = this.dataPointMaxWidth ? this.dataPointMaxWidth : Math.min(this.width * .15, this.plotArea.width / t.plotType.totalDataSeries * .9) << 0,
				rt = t.axisX.dataInfo.minDiff,
				c = h.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(rt) / t.plotType.totalDataSeries * .9 << 0;
			for (c > nt ? c = nt : rt === Infinity ? c = nt / t.plotType.totalDataSeries * .9 : c < 1 && (c = 1), v.save(), n && this._eventManager.ghostCtx.save(), v.beginPath(), v.rect(h.x1, h.y1, h.width, h.height), v.clip(), n && (this._eventManager.ghostCtx.rect(h.x1, h.y1, h.width, h.height), this._eventManager.ghostCtx.clip()), w = 0; w < t.dataSeriesIndexes.length; w++) {
				var ut = t.dataSeriesIndexes[w],
					l = this.data[ut],
					f = l.dataPoints;
				if (f.length > 0)
					for (tt = c > 5 && l.bevelEnabled ? !0 : !1, i = 0; i < f.length; i++)(b = f[i].getTime ? f[i].x.getTime() : f[i].x, b < t.axisX.dataInfo.viewPortMin || b > t.axisX.dataInfo.viewPortMax) || typeof f[i].y == "number" && (it = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (b - t.axisX.conversionParameters.minimum) + .5 << 0, d = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y - t.axisY.conversionParameters.minimum) + .5 << 0, a = it - t.plotType.totalDataSeries * c / 2 + (t.previousDataSeriesCount + w) * c << 0, y = a + c << 0, f[i].y >= 0 ? (e = d, s = g, e > s && (ft = e, e = s, s = e)) : (s = d, e = g, e > s && (ft = e, e = s, s = e)), p = f[i].color ? f[i].color : l._colorSet[i % l._colorSet.length], o(v, a, e, y, s, p, 0, null, tt && f[i].y >= 0, f[i].y < 0 && tt, !1, !1, l.fillOpacity), k = l.dataPointIds[i], this._eventManager.objectMap[k] = {
						id: k,
						objectType: "dataPoint",
						dataSeriesIndex: ut,
						dataPointIndex: i,
						x1: a,
						y1: e,
						x2: y,
						y2: s
					}, p = u(k), n && o(this._eventManager.ghostCtx, a, e, y, s, p, 0, null, !1, !1, !1, !1), (f[i].indexLabel || l.indexLabel || f[i].indexLabelFormatter || l.indexLabelFormatter) && this._indexLabels.push({
						chartType: "column",
						dataPoint: f[i],
						dataSeries: l,
						point: {
							x: a + (y - a) / 2,
							y: f[i].y >= 0 ? e : s
						},
						direction: f[i].y >= 0 ? 1 : -1,
						bounds: {
							x1: a,
							y1: Math.min(e, s),
							x2: y,
							y2: Math.max(e, s)
						},
						color: p
					}))
			}
			return v.restore(), n && this._eventManager.ghostCtx.restore(), et = Math.min(g, t.axisY.boundingRect.y2), ot = {
				source: v,
				dest: this.plotArea.ctx,
				animationCallback: r.yScaleAnimation,
				easingFunction: r.easing.easeOutQuart,
				animationBase: et
			}, ot
		}
	};
	t.prototype.renderStackedColumn = function(t) {
		var v = t.targetCanvasCtx || this.plotArea.ctx,
			ct = t.dataSeriesIndexes.length,
			k, ft, p, b, s, h, y, d, st, ht;
		if (!(ct <= 0)) {
			var w = null,
				c = this.plotArea,
				g = [],
				nt = [],
				i = 0,
				tt, it, e, rt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				ut = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.width * .15 << 0,
				et = t.axisX.dataInfo.minDiff,
				l = c.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(et) / t.plotType.plotUnits.length * .9 << 0;
			for (l > ut ? l = ut : et === Infinity ? l = ut : l < 1 && (l = 1), v.save(), n && this._eventManager.ghostCtx.save(), v.beginPath(), v.rect(c.x1, c.y1, c.width, c.height), v.clip(), n && (this._eventManager.ghostCtx.rect(c.x1, c.y1, c.width, c.height), this._eventManager.ghostCtx.clip()), k = 0; k < t.dataSeriesIndexes.length; k++) {
				var ot = t.dataSeriesIndexes[k],
					a = this.data[ot],
					f = a.dataPoints;
				if (f.length > 0)
					for (ft = l > 5 && a.bevelEnabled ? !0 : !1, v.strokeStyle = "#4572A7 ", i = 0; i < f.length; i++)(e = f[i].x.getTime ? f[i].x.getTime() : f[i].x, e < t.axisX.dataInfo.viewPortMin || e > t.axisX.dataInfo.viewPortMax) || typeof f[i].y == "number" && (tt = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (e - t.axisX.conversionParameters.minimum) + .5 << 0, it = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y - t.axisY.conversionParameters.minimum), p = tt - t.plotType.plotUnits.length * l / 2 + t.index * l << 0, b = p + l << 0, f[i].y >= 0 ? (y = g[e] ? g[e] : 0, s = it - y, h = rt - y, g[e] = y + (h - s)) : (y = nt[e] ? nt[e] : 0, h = it + y, s = rt + y, nt[e] = y + (h - s)), w = f[i].color ? f[i].color : a._colorSet[i % a._colorSet.length], o(v, p, s, b, h, w, 0, null, ft && f[i].y >= 0, f[i].y < 0 && ft, !1, !1, a.fillOpacity), d = a.dataPointIds[i], this._eventManager.objectMap[d] = {
						id: d,
						objectType: "dataPoint",
						dataSeriesIndex: ot,
						dataPointIndex: i,
						x1: p,
						y1: s,
						x2: b,
						y2: h
					}, w = u(d), n && o(this._eventManager.ghostCtx, p, s, b, h, w, 0, null, !1, !1, !1, !1), (f[i].indexLabel || a.indexLabel || f[i].indexLabelFormatter || a.indexLabelFormatter) && this._indexLabels.push({
						chartType: "stackedColumn",
						dataPoint: f[i],
						dataSeries: a,
						point: {
							x: tt,
							y: f[i].y >= 0 ? s : h
						},
						direction: f[i].y >= 0 ? 1 : -1,
						bounds: {
							x1: p,
							y1: Math.min(s, h),
							x2: b,
							y2: Math.max(s, h)
						},
						color: w
					}))
			}
			return v.restore(), n && this._eventManager.ghostCtx.restore(), st = Math.min(rt, t.axisY.boundingRect.y2), ht = {
				source: v,
				dest: this.plotArea.ctx,
				animationCallback: r.yScaleAnimation,
				easingFunction: r.easing.easeOutQuart,
				animationBase: st
			}, ht
		}
	};
	t.prototype.renderStackedColumn100 = function(t) {
		var y = t.targetCanvasCtx || this.plotArea.ctx,
			lt = t.dataSeriesIndexes.length,
			k, ft, st, p, b, s, h, v, d, ht, ct;
		if (!(lt <= 0)) {
			var w = null,
				c = this.plotArea,
				g = [],
				nt = [],
				i = 0,
				tt, it, e, rt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				ut = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.width * .15 << 0,
				et = t.axisX.dataInfo.minDiff,
				l = c.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(et) / t.plotType.plotUnits.length * .9 << 0;
			for (l > ut ? l = ut : et === Infinity ? l = ut : l < 1 && (l = 1), y.save(), n && this._eventManager.ghostCtx.save(), y.beginPath(), y.rect(c.x1, c.y1, c.width, c.height), y.clip(), n && (this._eventManager.ghostCtx.rect(c.x1, c.y1, c.width, c.height), this._eventManager.ghostCtx.clip()), k = 0; k < t.dataSeriesIndexes.length; k++) {
				var ot = t.dataSeriesIndexes[k],
					a = this.data[ot],
					f = a.dataPoints;
				if (f.length > 0)
					for (ft = l > 5 && a.bevelEnabled ? !0 : !1, i = 0; i < f.length; i++)(e = f[i].x.getTime ? f[i].x.getTime() : f[i].x, e < t.axisX.dataInfo.viewPortMin || e > t.axisX.dataInfo.viewPortMax) || typeof f[i].y == "number" && (tt = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (e - t.axisX.conversionParameters.minimum) + .5 << 0, st = t.dataPointYSums[e] !== 0 ? f[i].y / t.dataPointYSums[e] * 100 : 0, it = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (st - t.axisY.conversionParameters.minimum), p = tt - t.plotType.plotUnits.length * l / 2 + t.index * l << 0, b = p + l << 0, f[i].y >= 0 ? (v = g[e] ? g[e] : 0, s = it - v, h = rt - v, g[e] = v + (h - s)) : (v = nt[e] ? nt[e] : 0, h = it + v, s = rt + v, nt[e] = v + (h - s)), w = f[i].color ? f[i].color : a._colorSet[i % a._colorSet.length], o(y, p, s, b, h, w, 0, null, ft && f[i].y >= 0, f[i].y < 0 && ft, !1, !1, a.fillOpacity), d = a.dataPointIds[i], this._eventManager.objectMap[d] = {
						id: d,
						objectType: "dataPoint",
						dataSeriesIndex: ot,
						dataPointIndex: i,
						x1: p,
						y1: s,
						x2: b,
						y2: h
					}, w = u(d), n && o(this._eventManager.ghostCtx, p, s, b, h, w, 0, null, !1, !1, !1, !1), (f[i].indexLabel || a.indexLabel || f[i].indexLabelFormatter || a.indexLabelFormatter) && this._indexLabels.push({
						chartType: "stackedColumn100",
						dataPoint: f[i],
						dataSeries: a,
						point: {
							x: tt,
							y: f[i].y >= 0 ? s : h
						},
						direction: f[i].y >= 0 ? 1 : -1,
						bounds: {
							x1: p,
							y1: Math.min(s, h),
							x2: b,
							y2: Math.max(s, h)
						},
						color: w
					}))
			}
			return y.restore(), n && this._eventManager.ghostCtx.restore(), ht = Math.min(rt, t.axisY.boundingRect.y2), ct = {
				source: y,
				dest: this.plotArea.ctx,
				animationCallback: r.yScaleAnimation,
				easingFunction: r.easing.easeOutQuart,
				animationBase: ht
			}, ct
		}
	};
	t.prototype.renderBar = function(t) {
		var c = t.targetCanvasCtx || this.plotArea.ctx,
			ot = t.dataSeriesIndexes.length,
			w, ut, l, y, a, v, k, ft, et;
		if (!(ot <= 0)) {
			var p = null,
				e = this.plotArea,
				i = 0,
				d, tt, b, g = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				nt = this.dataPointMaxWidth ? this.dataPointMaxWidth : Math.min(this.height * .15, this.plotArea.height / t.plotType.totalDataSeries * .9) << 0,
				it = t.axisX.dataInfo.minDiff,
				s = e.height / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(it) / t.plotType.totalDataSeries * .9 << 0;
			for (s > nt ? s = nt : it === Infinity ? s = nt / t.plotType.totalDataSeries * .9 : s < 1 && (s = 1), c.save(), n && this._eventManager.ghostCtx.save(), c.beginPath(), c.rect(e.x1, e.y1, e.width, e.height), c.clip(), n && (this._eventManager.ghostCtx.rect(e.x1, e.y1, e.width, e.height), this._eventManager.ghostCtx.clip()), w = 0; w < t.dataSeriesIndexes.length; w++) {
				var rt = t.dataSeriesIndexes[w],
					h = this.data[rt],
					f = h.dataPoints;
				if (f.length > 0)
					for (ut = s > 5 && h.bevelEnabled ? !0 : !1, c.strokeStyle = "#4572A7 ", i = 0; i < f.length; i++)(b = f[i].getTime ? f[i].x.getTime() : f[i].x, b < t.axisX.dataInfo.viewPortMin || b > t.axisX.dataInfo.viewPortMax) || typeof f[i].y == "number" && (tt = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (b - t.axisX.conversionParameters.minimum) + .5 << 0, d = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y - t.axisY.conversionParameters.minimum) + .5 << 0, l = tt - t.plotType.totalDataSeries * s / 2 + (t.previousDataSeriesCount + w) * s << 0, y = l + s << 0, f[i].y >= 0 ? (a = g, v = d) : (a = d, v = g), p = f[i].color ? f[i].color : h._colorSet[i % h._colorSet.length], o(c, a, l, v, y, p, 0, null, ut, !1, !1, !1, h.fillOpacity), k = h.dataPointIds[i], this._eventManager.objectMap[k] = {
						id: k,
						objectType: "dataPoint",
						dataSeriesIndex: rt,
						dataPointIndex: i,
						x1: a,
						y1: l,
						x2: v,
						y2: y
					}, p = u(k), n && o(this._eventManager.ghostCtx, a, l, v, y, p, 0, null, !1, !1, !1, !1), (f[i].indexLabel || h.indexLabel || f[i].indexLabelFormatter || h.indexLabelFormatter) && this._indexLabels.push({
						chartType: "bar",
						dataPoint: f[i],
						dataSeries: h,
						point: {
							x: f[i].y >= 0 ? v : a,
							y: l + (y - l) / 2
						},
						direction: f[i].y >= 0 ? 1 : -1,
						bounds: {
							x1: Math.min(a, v),
							y1: l,
							x2: Math.max(a, v),
							y2: y
						},
						color: p
					}))
			}
			return c.restore(), n && this._eventManager.ghostCtx.restore(), ft = Math.max(g, t.axisX.boundingRect.x2), et = {
				source: c,
				dest: this.plotArea.ctx,
				animationCallback: r.xScaleAnimation,
				easingFunction: r.easing.easeOutQuart,
				animationBase: ft
			}, et
		}
	};
	t.prototype.renderStackedBar = function(t) {
		var v = t.targetCanvasCtx || this.plotArea.ctx,
			ct = t.dataSeriesIndexes.length,
			k, ot, p, b, s, h, y, d, st, ht;
		if (!(ct <= 0)) {
			var w = null,
				c = this.plotArea,
				g = [],
				nt = [],
				i = 0,
				tt, it, e, rt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				ut = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.height * .15 << 0,
				ft = t.axisX.dataInfo.minDiff,
				l = c.height / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(ft) / t.plotType.plotUnits.length * .9 << 0;
			for (l > ut ? l = ut : ft === Infinity ? l = ut : l < 1 && (l = 1), v.save(), n && this._eventManager.ghostCtx.save(), v.beginPath(), v.rect(c.x1, c.y1, c.width, c.height), v.clip(), n && (this._eventManager.ghostCtx.rect(c.x1, c.y1, c.width, c.height), this._eventManager.ghostCtx.clip()), k = 0; k < t.dataSeriesIndexes.length; k++) {
				var et = t.dataSeriesIndexes[k],
					a = this.data[et],
					f = a.dataPoints;
				if (f.length > 0)
					for (ot = l > 5 && a.bevelEnabled ? !0 : !1, v.strokeStyle = "#4572A7 ", i = 0; i < f.length; i++)(e = f[i].x.getTime ? f[i].x.getTime() : f[i].x, e < t.axisX.dataInfo.viewPortMin || e > t.axisX.dataInfo.viewPortMax) || typeof f[i].y == "number" && (it = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (e - t.axisX.conversionParameters.minimum) + .5 << 0, tt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y - t.axisY.conversionParameters.minimum), p = it - t.plotType.plotUnits.length * l / 2 + t.index * l << 0, b = p + l << 0, f[i].y >= 0 ? (y = g[e] ? g[e] : 0, s = rt + y, h = tt + y, g[e] = y + (h - s)) : (y = nt[e] ? nt[e] : 0, s = tt - y, h = rt - y, nt[e] = y + (h - s)), w = f[i].color ? f[i].color : a._colorSet[i % a._colorSet.length], o(v, s, p, h, b, w, 0, null, ot, !1, !1, !1, a.fillOpacity), d = a.dataPointIds[i], this._eventManager.objectMap[d] = {
						id: d,
						objectType: "dataPoint",
						dataSeriesIndex: et,
						dataPointIndex: i,
						x1: s,
						y1: p,
						x2: h,
						y2: b
					}, w = u(d), n && o(this._eventManager.ghostCtx, s, p, h, b, w, 0, null, !1, !1, !1, !1), (f[i].indexLabel || a.indexLabel || f[i].indexLabelFormatter || a.indexLabelFormatter) && this._indexLabels.push({
						chartType: "stackedBar",
						dataPoint: f[i],
						dataSeries: a,
						point: {
							x: f[i].y >= 0 ? h : s,
							y: it
						},
						direction: f[i].y >= 0 ? 1 : -1,
						bounds: {
							x1: Math.min(s, h),
							y1: p,
							x2: Math.max(s, h),
							y2: b
						},
						color: w
					}))
			}
			return v.restore(), n && this._eventManager.ghostCtx.restore(), st = Math.max(rt, t.axisX.boundingRect.x2), ht = {
				source: v,
				dest: this.plotArea.ctx,
				animationCallback: r.xScaleAnimation,
				easingFunction: r.easing.easeOutQuart,
				animationBase: st
			}, ht
		}
	};
	t.prototype.renderStackedBar100 = function(t) {
		var v = t.targetCanvasCtx || this.plotArea.ctx,
			lt = t.dataSeriesIndexes.length,
			k, ot, st, p, b, s, h, y, d, ht, ct;
		if (!(lt <= 0)) {
			var w = null,
				c = this.plotArea,
				g = [],
				nt = [],
				i = 0,
				tt, it, e, rt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				ut = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.height * .15 << 0,
				ft = t.axisX.dataInfo.minDiff,
				l = c.height / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(ft) / t.plotType.plotUnits.length * .9 << 0;
			for (l > ut ? l = ut : ft === Infinity ? l = ut : l < 1 && (l = 1), v.save(), n && this._eventManager.ghostCtx.save(), v.beginPath(), v.rect(c.x1, c.y1, c.width, c.height), v.clip(), n && (this._eventManager.ghostCtx.rect(c.x1, c.y1, c.width, c.height), this._eventManager.ghostCtx.clip()), k = 0; k < t.dataSeriesIndexes.length; k++) {
				var et = t.dataSeriesIndexes[k],
					a = this.data[et],
					f = a.dataPoints;
				if (f.length > 0)
					for (ot = l > 5 && a.bevelEnabled ? !0 : !1, v.strokeStyle = "#4572A7 ", i = 0; i < f.length; i++)(e = f[i].x.getTime ? f[i].x.getTime() : f[i].x, e < t.axisX.dataInfo.viewPortMin || e > t.axisX.dataInfo.viewPortMax) || typeof f[i].y == "number" && (it = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (e - t.axisX.conversionParameters.minimum) + .5 << 0, st = t.dataPointYSums[e] !== 0 ? f[i].y / t.dataPointYSums[e] * 100 : 0, tt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (st - t.axisY.conversionParameters.minimum), p = it - t.plotType.plotUnits.length * l / 2 + t.index * l << 0, b = p + l << 0, f[i].y >= 0 ? (y = g[e] ? g[e] : 0, s = rt + y, h = tt + y, g[e] = y + (h - s)) : (y = nt[e] ? nt[e] : 0, s = tt - y, h = rt - y, nt[e] = y + (h - s)), w = f[i].color ? f[i].color : a._colorSet[i % a._colorSet.length], o(v, s, p, h, b, w, 0, null, ot, !1, !1, !1, a.fillOpacity), d = a.dataPointIds[i], this._eventManager.objectMap[d] = {
						id: d,
						objectType: "dataPoint",
						dataSeriesIndex: et,
						dataPointIndex: i,
						x1: s,
						y1: p,
						x2: h,
						y2: b
					}, w = u(d), n && o(this._eventManager.ghostCtx, s, p, h, b, w, 0, null, !1, !1, !1, !1), (f[i].indexLabel || a.indexLabel || f[i].indexLabelFormatter || a.indexLabelFormatter) && this._indexLabels.push({
						chartType: "stackedBar100",
						dataPoint: f[i],
						dataSeries: a,
						point: {
							x: f[i].y >= 0 ? h : s,
							y: it
						},
						direction: f[i].y >= 0 ? 1 : -1,
						bounds: {
							x1: Math.min(s, h),
							y1: p,
							x2: Math.max(s, h),
							y2: b
						},
						color: w
					}))
			}
			return v.restore(), n && this._eventManager.ghostCtx.restore(), ht = Math.max(rt, t.axisX.boundingRect.x2), ct = {
				source: v,
				dest: this.plotArea.ctx,
				animationCallback: r.xScaleAnimation,
				easingFunction: r.easing.easeOutQuart,
				animationBase: ht
			}, ct
		}
	};
	t.prototype.renderArea = function(t) {
		function ut() {
			p && (o.lineThickness > 0 && i.stroke(), t.axisY.minimum <= 0 && t.axisY.maximum >= 0 ? v = lt : t.axisY.maximum < 0 ? v = ct.y1 : t.axisY.minimum > 0 && (v = ht.y2), i.lineTo(s, v), i.lineTo(p.x, v), i.closePath(), i.globalAlpha = o.fillOpacity, i.fill(), i.globalAlpha = 1, n && (e.lineTo(s, v), e.lineTo(p.x, v), e.closePath(), e.fill()), i.beginPath(), i.moveTo(s, c), e.beginPath(), e.moveTo(s, c), p = {
				x: s,
				y: c
			})
		}
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			st = t.dataSeriesIndexes.length,
			k, et, g, nt, tt, w, rt;
		if (!(st <= 0)) {
			var e = this._eventManager.ghostCtx,
				ht = t.axisX.lineCoordinates,
				ct = t.axisY.lineCoordinates,
				b = [],
				l = this.plotArea;
			for (i.save(), n && e.save(), i.beginPath(), i.rect(l.x1, l.y1, l.width, l.height), i.clip(), n && (e.beginPath(), e.rect(l.x1, l.y1, l.width, l.height), e.clip()), k = 0; k < t.dataSeriesIndexes.length; k++) {
				var it = t.dataSeriesIndexes[k],
					o = this.data[it],
					h = o.dataPoints,
					ft = o.id;
				this._eventManager.objectMap[ft] = {
					objectType: "dataSeries",
					dataSeriesIndex: it
				};
				et = u(ft);
				e.fillStyle = et;
				b = [];
				var ot = !0,
					f = 0,
					s, c, d, lt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) + .5 << 0,
					v, p = null;
				if (h.length > 0) {
					for (g = o._colorSet[f % o._colorSet.length], i.fillStyle = g, i.strokeStyle = g, i.lineWidth = o.lineThickness, i.setLineDash && i.setLineDash(y(o.lineDashType, o.lineThickness)), nt = !0; f < h.length; f++)
						if (d = h[f].x.getTime ? h[f].x.getTime() : h[f].x, !(d < t.axisX.dataInfo.viewPortMin) && !(d > t.axisX.dataInfo.viewPortMax)) {
							if (typeof h[f].y != "number") {
								ut();
								nt = !0;
								continue
							}
							s = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (d - t.axisX.conversionParameters.minimum) + .5 << 0;
							c = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (h[f].y - t.axisY.conversionParameters.minimum) + .5 << 0;
							ot || nt ? (i.beginPath(), i.moveTo(s, c), p = {
								x: s,
								y: c
							}, n && (e.beginPath(), e.moveTo(s, c)), ot = !1, nt = !1) : (i.lineTo(s, c), n && e.lineTo(s, c), f % 250 == 0 && ut());
							tt = o.dataPointIds[f];
							this._eventManager.objectMap[tt] = {
								id: tt,
								objectType: "dataPoint",
								dataSeriesIndex: it,
								dataPointIndex: f,
								x1: s,
								y1: c
							};
							h[f].markerSize !== 0 && (h[f].markerSize > 0 || o.markerSize > 0) && (w = o.getMarkerProperties(f, s, c, i), b.push(w), rt = u(tt), n && b.push({
								x: s,
								y: c,
								ctx: e,
								type: w.type,
								size: w.size,
								color: rt,
								borderColor: rt,
								borderThickness: w.borderThickness
							}));
							(h[f].indexLabel || o.indexLabel || h[f].indexLabelFormatter || o.indexLabelFormatter) && this._indexLabels.push({
								chartType: "area",
								dataPoint: h[f],
								dataSeries: o,
								point: {
									x: s,
									y: c
								},
								direction: h[f].y >= 0 ? 1 : -1,
								color: g
							})
						} ut();
					a.drawMarkers(b)
				}
			}
			return i.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderSplineArea = function(t) {
		function ft() {
			var r = kt(d, 2),
				u;
			if (r.length > 0) {
				for (i.beginPath(), i.moveTo(r[0].x, r[0].y), n && (o.beginPath(), o.moveTo(r[0].x, r[0].y)), u = 0; u < r.length - 3; u += 3) i.bezierCurveTo(r[u + 1].x, r[u + 1].y, r[u + 2].x, r[u + 2].y, r[u + 3].x, r[u + 3].y), n && o.bezierCurveTo(r[u + 1].x, r[u + 1].y, r[u + 2].x, r[u + 2].y, r[u + 3].x, r[u + 3].y);
				e.lineThickness > 0 && i.stroke();
				t.axisY.minimum <= 0 && t.axisY.maximum >= 0 ? c = ht : t.axisY.maximum < 0 ? c = st.y1 : t.axisY.minimum > 0 && (c = ot.y2);
				tt = {
					x: r[0].x,
					y: r[0].y
				};
				i.lineTo(r[r.length - 1].x, c);
				i.lineTo(tt.x, c);
				i.closePath();
				i.globalAlpha = e.fillOpacity;
				i.fill();
				i.globalAlpha = 1;
				n && (o.lineTo(r[r.length - 1].x, c), o.lineTo(tt.x, c), o.closePath(), o.fill())
			}
		}
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			et = t.dataSeriesIndexes.length,
			b, ut, g, p, it;
		if (!(et <= 0)) {
			var o = this._eventManager.ghostCtx,
				ot = t.axisX.lineCoordinates,
				st = t.axisY.lineCoordinates,
				w = [],
				h = this.plotArea;
			for (i.save(), n && o.save(), i.beginPath(), i.rect(h.x1, h.y1, h.width, h.height), i.clip(), n && (o.beginPath(), o.rect(h.x1, h.y1, h.width, h.height), o.clip()), b = 0; b < t.dataSeriesIndexes.length; b++) {
				var nt = t.dataSeriesIndexes[b],
					e = this.data[nt],
					s = e.dataPoints,
					rt = e.id;
				this._eventManager.objectMap[rt] = {
					objectType: "dataSeries",
					dataSeriesIndex: nt
				};
				ut = u(rt);
				o.fillStyle = ut;
				w = [];
				var f = 0,
					l, v, k, ht = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) + .5 << 0,
					c, tt = null,
					d = [];
				if (s.length > 0) {
					for (color = e._colorSet[f % e._colorSet.length], i.fillStyle = color, i.strokeStyle = color, i.lineWidth = e.lineThickness, i.setLineDash && i.setLineDash(y(e.lineDashType, e.lineThickness)); f < s.length; f++)
						if (k = s[f].x.getTime ? s[f].x.getTime() : s[f].x, !(k < t.axisX.dataInfo.viewPortMin) && !(k > t.axisX.dataInfo.viewPortMax)) {
							if (typeof s[f].y != "number") {
								f > 0 && (ft(), d = []);
								continue
							}
							l = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (k - t.axisX.conversionParameters.minimum) + .5 << 0;
							v = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (s[f].y - t.axisY.conversionParameters.minimum) + .5 << 0;
							g = e.dataPointIds[f];
							this._eventManager.objectMap[g] = {
								id: g,
								objectType: "dataPoint",
								dataSeriesIndex: nt,
								dataPointIndex: f,
								x1: l,
								y1: v
							};
							d[d.length] = {
								x: l,
								y: v
							};
							s[f].markerSize !== 0 && (s[f].markerSize > 0 || e.markerSize > 0) && (p = e.getMarkerProperties(f, l, v, i), w.push(p), it = u(g), n && w.push({
								x: l,
								y: v,
								ctx: o,
								type: p.type,
								size: p.size,
								color: it,
								borderColor: it,
								borderThickness: p.borderThickness
							}));
							(s[f].indexLabel || e.indexLabel || s[f].indexLabelFormatter || e.indexLabelFormatter) && this._indexLabels.push({
								chartType: "splineArea",
								dataPoint: s[f],
								dataSeries: e,
								point: {
									x: l,
									y: v
								},
								direction: s[f].y >= 0 ? 1 : -1,
								color: color
							})
						} ft();
					a.drawMarkers(w)
				}
			}
			return i.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderStepArea = function(t) {
		function ft() {
			p && (s.lineThickness > 0 && i.stroke(), t.axisY.minimum <= 0 && t.axisY.maximum >= 0 ? v = at : t.axisY.maximum < 0 ? v = lt.y1 : t.axisY.minimum > 0 && (v = ct.y2), i.lineTo(e, v), i.lineTo(p.x, v), i.closePath(), i.globalAlpha = s.fillOpacity, i.fill(), i.globalAlpha = 1, n && (o.lineTo(e, v), o.lineTo(p.x, v), o.closePath(), o.fill()), i.beginPath(), i.moveTo(e, h), o.beginPath(), o.moveTo(e, h), p = {
				x: e,
				y: h
			})
		}
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			ht = t.dataSeriesIndexes.length,
			k, ot, g, rt, nt, w, ut;
		if (!(ht <= 0)) {
			var o = this._eventManager.ghostCtx,
				ct = t.axisX.lineCoordinates,
				lt = t.axisY.lineCoordinates,
				b = [],
				l = this.plotArea;
			for (i.save(), n && o.save(), i.beginPath(), i.rect(l.x1, l.y1, l.width, l.height), i.clip(), n && (o.beginPath(), o.rect(l.x1, l.y1, l.width, l.height), o.clip()), k = 0; k < t.dataSeriesIndexes.length; k++) {
				var tt = t.dataSeriesIndexes[k],
					s = this.data[tt],
					c = s.dataPoints,
					et = s.id;
				this._eventManager.objectMap[et] = {
					objectType: "dataSeries",
					dataSeriesIndex: tt
				};
				ot = u(et);
				o.fillStyle = ot;
				b = [];
				var st = !0,
					f = 0,
					e, h, d, at = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) + .5 << 0,
					v, p = null,
					it = !1;
				if (c.length > 0) {
					for (g = s._colorSet[f % s._colorSet.length], i.fillStyle = g, i.strokeStyle = g, i.lineWidth = s.lineThickness, i.setLineDash && i.setLineDash(y(s.lineDashType, s.lineThickness)); f < c.length; f++)
						if (d = c[f].x.getTime ? c[f].x.getTime() : c[f].x, !(d < t.axisX.dataInfo.viewPortMin) && !(d > t.axisX.dataInfo.viewPortMax)) {
							if (rt = h, typeof c[f].y != "number") {
								ft();
								it = !0;
								continue
							}
							e = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (d - t.axisX.conversionParameters.minimum) + .5 << 0;
							h = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (c[f].y - t.axisY.conversionParameters.minimum) + .5 << 0;
							st || it ? (i.beginPath(), i.moveTo(e, h), p = {
								x: e,
								y: h
							}, n && (o.beginPath(), o.moveTo(e, h)), st = !1, it = !1) : (i.lineTo(e, rt), n && o.lineTo(e, rt), i.lineTo(e, h), n && o.lineTo(e, h), f % 250 == 0 && ft());
							nt = s.dataPointIds[f];
							this._eventManager.objectMap[nt] = {
								id: nt,
								objectType: "dataPoint",
								dataSeriesIndex: tt,
								dataPointIndex: f,
								x1: e,
								y1: h
							};
							c[f].markerSize !== 0 && (c[f].markerSize > 0 || s.markerSize > 0) && (w = s.getMarkerProperties(f, e, h, i), b.push(w), ut = u(nt), n && b.push({
								x: e,
								y: h,
								ctx: o,
								type: w.type,
								size: w.size,
								color: ut,
								borderColor: ut,
								borderThickness: w.borderThickness
							}));
							(c[f].indexLabel || s.indexLabel || c[f].indexLabelFormatter || s.indexLabelFormatter) && this._indexLabels.push({
								chartType: "stepArea",
								dataPoint: c[f],
								dataSeries: s,
								point: {
									x: e,
									y: h
								},
								direction: c[f].y >= 0 ? 1 : -1,
								color: g
							})
						} ft();
					a.drawMarkers(b)
				}
			}
			return i.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderStackedArea = function(t) {
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			lt = t.dataSeriesIndexes.length,
			w, ct, v, rt, nt, l;
		if (!(lt <= 0)) {
			var tt = null,
				ut = [],
				p = this.plotArea,
				ft = [],
				d = [],
				s = 0,
				h, o, c, et = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				at = t.axisX.dataInfo.minDiff,
				e = this._eventManager.ghostCtx;
			for (n && e.beginPath(), i.save(), n && e.save(), i.beginPath(), i.rect(p.x1, p.y1, p.width, p.height), i.clip(), n && (e.beginPath(), e.rect(p.x1, p.y1, p.width, p.height), e.clip()), xValuePresent = [], w = 0; w < t.dataSeriesIndexes.length; w++) {
				var it = t.dataSeriesIndexes[w],
					f = this.data[it],
					b = f.dataPoints,
					g;
				for (f.dataPointIndexes = [], s = 0; s < b.length; s++) g = b[s].x.getTime ? b[s].x.getTime() : b[s].x, f.dataPointIndexes[g] = s, xValuePresent[g] || (d.push(g), xValuePresent[g] = !0);
				d.sort(si)
			}
			for (w = 0; w < t.dataSeriesIndexes.length; w++) {
				var it = t.dataSeriesIndexes[w],
					f = this.data[it],
					b = f.dataPoints,
					st = !0,
					k = [],
					ht = f.id;
				if (this._eventManager.objectMap[ht] = {
						objectType: "dataSeries",
						dataSeriesIndex: it
					}, ct = u(ht), e.fillStyle = ct, d.length > 0) {
					for (tt = f._colorSet[0], i.fillStyle = tt, i.strokeStyle = tt, i.lineWidth = f.lineThickness, i.setLineDash && i.setLineDash(y(f.lineDashType, f.lineThickness)), s = 0; s < d.length; s++)
						if ((c = d[s], v = null, v = f.dataPointIndexes[c] >= 0 ? b[f.dataPointIndexes[c]] : {
								x: c,
								y: 0
							}, !(c < t.axisX.dataInfo.viewPortMin) && !(c > t.axisX.dataInfo.viewPortMax)) && typeof v.y == "number") {
							var h = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (c - t.axisX.conversionParameters.minimum) + .5 << 0,
								o = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (v.y - t.axisY.conversionParameters.minimum),
								ot = ft[c] ? ft[c] : 0;
							if (o = o - ot, k.push({
									x: h,
									y: et - ot
								}), ft[c] = et - o, st) i.beginPath(), i.moveTo(h, o), n && (e.beginPath(), e.moveTo(h, o)), st = !1;
							else if (i.lineTo(h, o), n && e.lineTo(h, o), s % 250 == 0) {
								for (f.lineThickness > 0 && i.stroke(); k.length > 0;) l = k.pop(), i.lineTo(l.x, l.y), n && e.lineTo(l.x, l.y);
								i.closePath();
								i.globalAlpha = f.fillOpacity;
								i.fill();
								i.globalAlpha = 1;
								i.beginPath();
								i.moveTo(h, o);
								n && (e.closePath(), e.fill(), e.beginPath(), e.moveTo(h, o));
								k.push({
									x: h,
									y: et - ot
								})
							}
							f.dataPointIndexes[c] >= 0 && (rt = f.dataPointIds[f.dataPointIndexes[c]], this._eventManager.objectMap[rt] = {
								id: rt,
								objectType: "dataPoint",
								dataSeriesIndex: it,
								dataPointIndex: f.dataPointIndexes[c],
								x1: h,
								y1: o
							});
							f.dataPointIndexes[c] >= 0 && v.markerSize !== 0 && (v.markerSize > 0 || f.markerSize > 0) && (nt = f.getMarkerProperties(s, h, o, i), ut.push(nt), markerColor = u(rt), n && ut.push({
								x: h,
								y: o,
								ctx: e,
								type: nt.type,
								size: nt.size,
								color: markerColor,
								borderColor: markerColor,
								borderThickness: nt.borderThickness
							}));
							(v.indexLabel || f.indexLabel || v.indexLabelFormatter || f.indexLabelFormatter) && this._indexLabels.push({
								chartType: "stackedArea",
								dataPoint: v,
								dataSeries: f,
								point: {
									x: h,
									y: o
								},
								direction: b[s].y >= 0 ? 1 : -1,
								color: tt
							})
						} for (f.lineThickness > 0 && i.stroke(); k.length > 0;) l = k.pop(), i.lineTo(l.x, l.y), n && e.lineTo(l.x, l.y);
					i.closePath();
					i.globalAlpha = f.fillOpacity;
					i.fill();
					i.globalAlpha = 1;
					i.beginPath();
					i.moveTo(h, o);
					n && (e.closePath(), e.fill(), e.beginPath(), e.moveTo(h, o))
				}
				delete f.dataPointIndexes
			}
			return a.drawMarkers(ut), i.restore(), n && e.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderStackedArea100 = function(t) {
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			yt = t.dataSeriesIndexes.length,
			w, at, k, wt, p, vt, ut, tt, l;
		if (!(yt <= 0)) {
			var it = null,
				v = this.plotArea,
				ft = [],
				et = [],
				g = [],
				s = 0,
				c, h, o, ot = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				st = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.width * .15 << 0,
				pt = t.axisX.dataInfo.minDiff,
				d = v.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(pt) * .9 << 0,
				e = this._eventManager.ghostCtx;
			for (i.save(), n && e.save(), i.beginPath(), i.rect(v.x1, v.y1, v.width, v.height), i.clip(), n && (e.beginPath(), e.rect(v.x1, v.y1, v.width, v.height), e.clip()), xValuePresent = [], w = 0; w < t.dataSeriesIndexes.length; w++) {
				var rt = t.dataSeriesIndexes[w],
					f = this.data[rt],
					b = f.dataPoints,
					nt;
				for (f.dataPointIndexes = [], s = 0; s < b.length; s++) nt = b[s].x.getTime ? b[s].x.getTime() : b[s].x, f.dataPointIndexes[nt] = s, xValuePresent[nt] || (g.push(nt), xValuePresent[nt] = !0);
				g.sort(si)
			}
			for (w = 0; w < t.dataSeriesIndexes.length; w++) {
				var rt = t.dataSeriesIndexes[w],
					f = this.data[rt],
					b = f.dataPoints,
					ct = !0,
					lt = f.id;
				if (this._eventManager.objectMap[lt] = {
						objectType: "dataSeries",
						dataSeriesIndex: rt
					}, at = u(lt), e.fillStyle = at, b.length == 1 && (d = st), d < 1 ? d = 1 : d > st && (d = st), k = [], g.length > 0) {
					for (it = f._colorSet[s % f._colorSet.length], i.fillStyle = it, i.strokeStyle = it, i.lineWidth = f.lineThickness, i.setLineDash && i.setLineDash(y(f.lineDashType, f.lineThickness)), wt = d > 5 ? !1 : !1, s = 0; s < g.length; s++)
						if ((o = g[s], p = null, p = f.dataPointIndexes[o] >= 0 ? b[f.dataPointIndexes[o]] : {
								x: o,
								y: 0
							}, !(o < t.axisX.dataInfo.viewPortMin) && !(o > t.axisX.dataInfo.viewPortMax)) && typeof p.y == "number") {
							vt = t.dataPointYSums[o] !== 0 ? p.y / t.dataPointYSums[o] * 100 : 0;
							var c = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (o - t.axisX.conversionParameters.minimum) + .5 << 0,
								h = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (vt - t.axisY.conversionParameters.minimum),
								ht = et[o] ? et[o] : 0;
							if (h = h - ht, k.push({
									x: c,
									y: ot - ht
								}), et[o] = ot - h, ct) i.beginPath(), i.moveTo(c, h), n && (e.beginPath(), e.moveTo(c, h)), ct = !1;
							else if (i.lineTo(c, h), n && e.lineTo(c, h), s % 250 == 0) {
								for (f.lineThickness > 0 && i.stroke(); k.length > 0;) l = k.pop(), i.lineTo(l.x, l.y), n && e.lineTo(l.x, l.y);
								i.closePath();
								i.globalAlpha = f.fillOpacity;
								i.fill();
								i.globalAlpha = 1;
								i.beginPath();
								i.moveTo(c, h);
								n && (e.closePath(), e.fill(), e.beginPath(), e.moveTo(c, h));
								k.push({
									x: c,
									y: ot - ht
								})
							}
							f.dataPointIndexes[o] >= 0 && (ut = f.dataPointIds[f.dataPointIndexes[o]], this._eventManager.objectMap[ut] = {
								id: ut,
								objectType: "dataPoint",
								dataSeriesIndex: rt,
								dataPointIndex: f.dataPointIndexes[o],
								x1: c,
								y1: h
							});
							f.dataPointIndexes[o] >= 0 && p.markerSize !== 0 && (p.markerSize > 0 || f.markerSize > 0) && (tt = f.getMarkerProperties(s, c, h, i), ft.push(tt), markerColor = u(ut), n && ft.push({
								x: c,
								y: h,
								ctx: e,
								type: tt.type,
								size: tt.size,
								color: markerColor,
								borderColor: markerColor,
								borderThickness: tt.borderThickness
							}));
							(p.indexLabel || f.indexLabel || p.indexLabelFormatter || f.indexLabelFormatter) && this._indexLabels.push({
								chartType: "stackedArea100",
								dataPoint: p,
								dataSeries: f,
								point: {
									x: c,
									y: h
								},
								direction: b[s].y >= 0 ? 1 : -1,
								color: it
							})
						} for (f.lineThickness > 0 && i.stroke(); k.length > 0;) l = k.pop(), i.lineTo(l.x, l.y), n && e.lineTo(l.x, l.y);
					i.closePath();
					i.globalAlpha = f.fillOpacity;
					i.fill();
					i.globalAlpha = 1;
					i.beginPath();
					i.moveTo(c, h);
					n && (e.closePath(), e.fill(), e.beginPath(), e.moveTo(c, h))
				}
				delete f.dataPointIndexes
			}
			return a.drawMarkers(ft), i.restore(), n && e.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderBubble = function(t) {
		var o = t.targetCanvasCtx || this.plotArea.ctx,
			ut = t.dataSeriesIndexes.length,
			k, p, d, it, c, i, g, rt;
		if (!(ut <= 0)) {
			var s = this.plotArea,
				i = 0,
				v, y, h, ht = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				nt = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.width * .15 << 0,
				et = t.axisX.dataInfo.minDiff,
				b = s.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(et) / ut * .9 << 0;
			for (o.save(), n && this._eventManager.ghostCtx.save(), o.beginPath(), o.rect(s.x1, s.y1, s.width, s.height), o.clip(), n && (this._eventManager.ghostCtx.rect(s.x1, s.y1, s.width, s.height), this._eventManager.ghostCtx.clip()), k = -Infinity, p = Infinity, c = 0; c < t.dataSeriesIndexes.length; c++) {
				var tt = t.dataSeriesIndexes[c],
					l = this.data[tt],
					f = l.dataPoints,
					w = 0;
				for (i = 0; i < f.length; i++)(h = h = f[i].getTime ? f[i].x.getTime() : f[i].x, h < t.axisX.dataInfo.viewPortMin || h > t.axisX.dataInfo.viewPortMax) || typeof f[i].z != "undefined" && (w = f[i].z, w > k && (k = w), w < p && (p = w))
			}
			for (d = Math.PI * 25, it = Math.max(Math.pow(Math.min(s.height, s.width) * .25 / 2, 2) * Math.PI, d), c = 0; c < t.dataSeriesIndexes.length; c++) {
				var tt = t.dataSeriesIndexes[c],
					l = this.data[tt],
					f = l.dataPoints;
				if (f.length == 1 && (b = nt), b < 1 ? b = 1 : b > nt && (b = nt), f.length > 0)
					for (o.strokeStyle = "#4572A7 ", i = 0; i < f.length; i++)
						if ((h = h = f[i].getTime ? f[i].x.getTime() : f[i].x, !(h < t.axisX.dataInfo.viewPortMin) && !(h > t.axisX.dataInfo.viewPortMax)) && typeof f[i].y == "number") {
							v = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (h - t.axisX.conversionParameters.minimum) + .5 << 0;
							y = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y - t.axisY.conversionParameters.minimum) + .5 << 0;
							var w = f[i].z,
								ot = k === p ? it / 2 : d + (it - d) / (k - p) * (w - p),
								st = Math.max(Math.sqrt(ot / Math.PI) << 0, 1),
								ft = st * 2,
								e = l.getMarkerProperties(i, o);
							e.size = ft;
							o.globalAlpha = l.fillOpacity;
							a.drawMarker(v, y, o, e.type, e.size, e.color, e.borderColor, e.borderThickness);
							o.globalAlpha = 1;
							g = l.dataPointIds[i];
							this._eventManager.objectMap[g] = {
								id: g,
								objectType: "dataPoint",
								dataSeriesIndex: tt,
								dataPointIndex: i,
								x1: v,
								y1: y,
								size: ft
							};
							rt = u(g);
							n && a.drawMarker(v, y, this._eventManager.ghostCtx, e.type, e.size, rt, rt, e.borderThickness);
							(f[i].indexLabel || l.indexLabel || f[i].indexLabelFormatter || l.indexLabelFormatter) && this._indexLabels.push({
								chartType: "bubble",
								dataPoint: f[i],
								dataSeries: l,
								point: {
									x: v,
									y: y
								},
								direction: 1,
								bounds: {
									x1: v - e.size / 2,
									y1: y - e.size / 2,
									x2: v + e.size / 2,
									y2: y + e.size / 2
								},
								color: null
							})
						}
			}
			return o.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: o,
				dest: this.plotArea.ctx,
				animationCallback: r.fadeInAnimation,
				easingFunction: r.easing.easeInQuad,
				animationBase: 0
			}
		}
	};
	t.prototype.renderScatter = function(t) {
		var s = t.targetCanvasCtx || this.plotArea.ctx,
			nt = t.dataSeriesIndexes.length,
			p, f, i, w, g;
		if (!(nt <= 0)) {
			var o = this.plotArea,
				f = 0,
				h, c, v, rt = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				b = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.width * .15 << 0,
				it = t.axisX.dataInfo.minDiff,
				y = o.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(it) / nt * .9 << 0;
			for (s.save(), n && this._eventManager.ghostCtx.save(), s.beginPath(), s.rect(o.x1, o.y1, o.width, o.height), s.clip(), n && (this._eventManager.ghostCtx.rect(o.x1, o.y1, o.width, o.height), this._eventManager.ghostCtx.clip()), p = 0; p < t.dataSeriesIndexes.length; p++) {
				var tt = t.dataSeriesIndexes[p],
					l = this.data[tt],
					e = l.dataPoints;
				if (e.length == 1 && (y = b), y < 1 ? y = 1 : y > b && (y = b), e.length > 0) {
					s.strokeStyle = "#4572A7 ";
					var ut = Math.pow(Math.min(o.height, o.width) * .3 / 2, 2) * Math.PI,
						k = 0,
						d = 0;
					for (f = 0; f < e.length; f++)(v = v = e[f].getTime ? e[f].x.getTime() : e[f].x, v < t.axisX.dataInfo.viewPortMin || v > t.axisX.dataInfo.viewPortMax) || typeof e[f].y == "number" && ((h = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (v - t.axisX.conversionParameters.minimum) + .5 << 0, c = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (e[f].y - t.axisY.conversionParameters.minimum) + .5 << 0, i = l.getMarkerProperties(f, h, c, s), s.globalAlpha = l.fillOpacity, a.drawMarker(i.x, i.y, i.ctx, i.type, i.size, i.color, i.borderColor, i.borderThickness), s.globalAlpha = 1, Math.sqrt((k - h) * (k - h) + (d - c) * (d - c)) < Math.min(i.size, 5) && e.length > Math.min(this.plotArea.width, this.plotArea.height)) || (w = l.dataPointIds[f], this._eventManager.objectMap[w] = {
						id: w,
						objectType: "dataPoint",
						dataSeriesIndex: tt,
						dataPointIndex: f,
						x1: h,
						y1: c
					}, g = u(w), n && a.drawMarker(i.x, i.y, this._eventManager.ghostCtx, i.type, i.size, g, g, i.borderThickness), (e[f].indexLabel || l.indexLabel || e[f].indexLabelFormatter || l.indexLabelFormatter) && this._indexLabels.push({
						chartType: "scatter",
						dataPoint: e[f],
						dataSeries: l,
						point: {
							x: h,
							y: c
						},
						direction: 1,
						bounds: {
							x1: h - i.size / 2,
							y1: c - i.size / 2,
							x2: h + i.size / 2,
							y2: c + i.size / 2
						},
						color: null
					}), k = h, d = c))
				}
			}
			return s.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: s,
				dest: this.plotArea.ctx,
				animationCallback: r.fadeInAnimation,
				easingFunction: r.easing.easeInQuad,
				animationBase: 0
			}
		}
	};
	t.prototype.renderCandlestick = function(t) {
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			f = this._eventManager.ghostCtx,
			st = t.dataSeriesIndexes.length,
			it, ft, p, g;
		if (!(st <= 0)) {
			var y = null,
				w = this.plotArea,
				e = 0,
				h, c, b, d, l, tt, ht = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				ut = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.width * .015,
				et = t.axisX.dataInfo.minDiff,
				k = w.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(et) * .7 << 0;
			for (k > ut ? k = ut : et === Infinity ? k = ut : k < 1 && (k = 1), i.save(), n && f.save(), i.beginPath(), i.rect(w.x1, w.y1, w.width, w.height), i.clip(), n && (f.rect(w.x1, w.y1, w.width, w.height), f.clip()), it = 0; it < t.dataSeriesIndexes.length; it++) {
				var ot = t.dataSeriesIndexes[it],
					a = this.data[ot],
					s = a.dataPoints;
				if (s.length > 0)
					for (ft = k > 5 && a.bevelEnabled ? !0 : !1, e = 0; e < s.length; e++)
						if ((tt = s[e].getTime ? s[e].x.getTime() : s[e].x, !(tt < t.axisX.dataInfo.viewPortMin) && !(tt > t.axisX.dataInfo.viewPortMax)) && s[e].y !== null && s[e].y.length && typeof s[e].y[0] == "number" && typeof s[e].y[1] == "number" && typeof s[e].y[2] == "number" && typeof s[e].y[3] == "number") {
							h = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (tt - t.axisX.conversionParameters.minimum) + .5 << 0;
							c = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (s[e].y[0] - t.axisY.conversionParameters.minimum) + .5 << 0;
							b = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (s[e].y[1] - t.axisY.conversionParameters.minimum) + .5 << 0;
							d = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (s[e].y[2] - t.axisY.conversionParameters.minimum) + .5 << 0;
							l = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (s[e].y[3] - t.axisY.conversionParameters.minimum) + .5 << 0;
							p = h - k / 2 << 0;
							g = p + k << 0;
							y = s[e].color ? s[e].color : a._colorSet[0];
							var nt = Math.round(Math.max(1, k * .15)),
								v = nt % 2 == 0 ? 0 : .5,
								rt = a.dataPointIds[e];
							this._eventManager.objectMap[rt] = {
								id: rt,
								objectType: "dataPoint",
								dataSeriesIndex: ot,
								dataPointIndex: e,
								x1: p,
								y1: c,
								x2: g,
								y2: b,
								x3: h,
								y3: d,
								x4: h,
								y4: l,
								borderThickness: nt,
								color: y
							};
							i.strokeStyle = y;
							i.beginPath();
							i.lineWidth = nt;
							f.lineWidth = Math.max(nt, 4);
							a.type === "candlestick" ? (i.moveTo(h - v, b), i.lineTo(h - v, Math.min(c, l)), i.stroke(), i.moveTo(h - v, Math.max(c, l)), i.lineTo(h - v, d), i.stroke(), o(i, p, Math.min(c, l), g, Math.max(c, l), s[e].y[0] <= s[e].y[3] ? a.risingColor : y, nt, y, ft, ft, !1, !1, a.fillOpacity), n && (y = u(rt), f.strokeStyle = y, f.moveTo(h - v, b), f.lineTo(h - v, Math.min(c, l)), f.stroke(), f.moveTo(h - v, Math.max(c, l)), f.lineTo(h - v, d), f.stroke(), o(f, p, Math.min(c, l), g, Math.max(c, l), y, 0, null, !1, !1, !1, !1))) : a.type === "ohlc" && (i.moveTo(h - v, b), i.lineTo(h - v, d), i.stroke(), i.beginPath(), i.moveTo(h, c), i.lineTo(p, c), i.stroke(), i.beginPath(), i.moveTo(h, l), i.lineTo(g, l), i.stroke(), n && (y = u(rt), f.strokeStyle = y, f.moveTo(h - v, b), f.lineTo(h - v, d), f.stroke(), f.beginPath(), f.moveTo(h, c), f.lineTo(p, c), f.stroke(), f.beginPath(), f.moveTo(h, l), f.lineTo(g, l), f.stroke()));
							(s[e].indexLabel || a.indexLabel || s[e].indexLabelFormatter || a.indexLabelFormatter) && this._indexLabels.push({
								chartType: a.type,
								dataPoint: s[e],
								dataSeries: a,
								point: {
									x: p + (g - p) / 2,
									y: b
								},
								direction: 1,
								bounds: {
									x1: p,
									y1: Math.min(b, d),
									x2: g,
									y2: Math.max(b, d)
								},
								color: y
							})
						}
			}
			return i.restore(), n && f.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.fadeInAnimation,
				easingFunction: r.easing.easeInQuad,
				animationBase: 0
			}
		}
	};
	t.prototype.renderRangeColumn = function(t) {
		var y = t.targetCanvasCtx || this.plotArea.ctx,
			ft = t.dataSeriesIndexes.length,
			w, g, h, v, e, s, rt, k, ut;
		if (!(ft <= 0)) {
			var p = null,
				l = this.plotArea,
				i = 0,
				nt, e, s, b, et = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				d = this.dataPointMaxWidth ? this.dataPointMaxWidth : this.width * .03,
				tt = t.axisX.dataInfo.minDiff,
				a = l.width / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(tt) / t.plotType.totalDataSeries * .9 << 0;
			for (a > d ? a = d : tt === Infinity ? a = d / t.plotType.totalDataSeries * .9 : a < 1 && (a = 1), y.save(), n && this._eventManager.ghostCtx.save(), y.beginPath(), y.rect(l.x1, l.y1, l.width, l.height), y.clip(), n && (this._eventManager.ghostCtx.rect(l.x1, l.y1, l.width, l.height), this._eventManager.ghostCtx.clip()), w = 0; w < t.dataSeriesIndexes.length; w++) {
				var it = t.dataSeriesIndexes[w],
					c = this.data[it],
					f = c.dataPoints;
				if (f.length > 0)
					for (g = a > 5 && c.bevelEnabled ? !0 : !1, i = 0; i < f.length; i++)(b = f[i].getTime ? f[i].x.getTime() : f[i].x, b < t.axisX.dataInfo.viewPortMin || b > t.axisX.dataInfo.viewPortMax) || f[i].y !== null && f[i].y.length && typeof f[i].y[0] == "number" && typeof f[i].y[1] == "number" && (nt = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (b - t.axisX.conversionParameters.minimum) + .5 << 0, e = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y[0] - t.axisY.conversionParameters.minimum) + .5 << 0, s = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y[1] - t.axisY.conversionParameters.minimum) + .5 << 0, h = nt - t.plotType.totalDataSeries * a / 2 + (t.previousDataSeriesCount + w) * a << 0, v = h + a << 0, p = f[i].color ? f[i].color : c._colorSet[i % c._colorSet.length], e > s && (rt = e, e = s, s = rt), k = c.dataPointIds[i], this._eventManager.objectMap[k] = {
						id: k,
						objectType: "dataPoint",
						dataSeriesIndex: it,
						dataPointIndex: i,
						x1: h,
						y1: e,
						x2: v,
						y2: s
					}, ut = 0, o(y, h, e, v, s, p, ut, p, g, g, !1, !1, c.fillOpacity), p = u(k), n && o(this._eventManager.ghostCtx, h, e, v, s, p, 0, null, !1, !1, !1, !1), (f[i].indexLabel || c.indexLabel || f[i].indexLabelFormatter || c.indexLabelFormatter) && (this._indexLabels.push({
						chartType: "rangeColumn",
						dataPoint: f[i],
						dataSeries: c,
						indexKeyword: 0,
						point: {
							x: h + (v - h) / 2,
							y: f[i].y[1] >= f[i].y[0] ? s : e
						},
						direction: f[i].y[1] >= f[i].y[0] ? -1 : 1,
						bounds: {
							x1: h,
							y1: Math.min(e, s),
							x2: v,
							y2: Math.max(e, s)
						},
						color: p
					}), this._indexLabels.push({
						chartType: "rangeColumn",
						dataPoint: f[i],
						dataSeries: c,
						indexKeyword: 1,
						point: {
							x: h + (v - h) / 2,
							y: f[i].y[1] >= f[i].y[0] ? e : s
						},
						direction: f[i].y[1] >= f[i].y[0] ? 1 : -1,
						bounds: {
							x1: h,
							y1: Math.min(e, s),
							x2: v,
							y2: Math.max(e, s)
						},
						color: p
					})))
			}
			return y.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: y,
				dest: this.plotArea.ctx,
				animationCallback: r.fadeInAnimation,
				easingFunction: r.easing.easeInQuad,
				animationBase: 0
			}
		}
	};
	t.prototype.renderRangeBar = function(t) {
		var v = t.targetCanvasCtx || this.plotArea.ctx,
			ut = t.dataSeriesIndexes.length,
			w, it, h, y, rt, k;
		if (!(ut <= 0)) {
			var p = null,
				l = this.plotArea,
				i = 0,
				e, s, g, b, ft = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) << 0,
				d = this.dataPointMaxWidth ? this.dataPointMaxWidth : Math.min(this.height * .15, this.plotArea.height / t.plotType.totalDataSeries * .9) << 0,
				nt = t.axisX.dataInfo.minDiff,
				a = l.height / Math.abs(t.axisX.maximum - t.axisX.minimum) * Math.abs(nt) / t.plotType.totalDataSeries * .9 << 0;
			for (a > d ? a = d : nt === Infinity ? a = d / t.plotType.totalDataSeries * .9 : a < 1 && (a = 1), v.save(), n && this._eventManager.ghostCtx.save(), v.beginPath(), v.rect(l.x1, l.y1, l.width, l.height), v.clip(), n && (this._eventManager.ghostCtx.rect(l.x1, l.y1, l.width, l.height), this._eventManager.ghostCtx.clip()), w = 0; w < t.dataSeriesIndexes.length; w++) {
				var tt = t.dataSeriesIndexes[w],
					c = this.data[tt],
					f = c.dataPoints;
				if (f.length > 0)
					for (it = a > 5 && c.bevelEnabled ? !0 : !1, v.strokeStyle = "#4572A7 ", i = 0; i < f.length; i++)(b = f[i].getTime ? f[i].x.getTime() : f[i].x, b < t.axisX.dataInfo.viewPortMin || b > t.axisX.dataInfo.viewPortMax) || f[i].y !== null && f[i].y.length && typeof f[i].y[0] == "number" && typeof f[i].y[1] == "number" && (e = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y[0] - t.axisY.conversionParameters.minimum) + .5 << 0, s = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (f[i].y[1] - t.axisY.conversionParameters.minimum) + .5 << 0, g = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (b - t.axisX.conversionParameters.minimum) + .5 << 0, h = g - t.plotType.totalDataSeries * a / 2 + (t.previousDataSeriesCount + w) * a << 0, y = h + a << 0, e > s && (rt = e, e = s, s = rt), p = f[i].color ? f[i].color : c._colorSet[i % c._colorSet.length], o(v, e, h, s, y, p, 0, null, it, !1, !1, !1, c.fillOpacity), k = c.dataPointIds[i], this._eventManager.objectMap[k] = {
						id: k,
						objectType: "dataPoint",
						dataSeriesIndex: tt,
						dataPointIndex: i,
						x1: e,
						y1: h,
						x2: s,
						y2: y
					}, p = u(k), n && o(this._eventManager.ghostCtx, e, h, s, y, p, 0, null, !1, !1, !1, !1), (f[i].indexLabel || c.indexLabel || f[i].indexLabelFormatter || c.indexLabelFormatter) && (this._indexLabels.push({
						chartType: "rangeBar",
						dataPoint: f[i],
						dataSeries: c,
						indexKeyword: 0,
						point: {
							x: f[i].y[1] >= f[i].y[0] ? e : s,
							y: h + (y - h) / 2
						},
						direction: f[i].y[1] >= f[i].y[0] ? -1 : 1,
						bounds: {
							x1: Math.min(e, s),
							y1: h,
							x2: Math.max(e, s),
							y2: y
						},
						color: p
					}), this._indexLabels.push({
						chartType: "rangeBar",
						dataPoint: f[i],
						dataSeries: c,
						indexKeyword: 1,
						point: {
							x: f[i].y[1] >= f[i].y[0] ? s : e,
							y: h + (y - h) / 2
						},
						direction: f[i].y[1] >= f[i].y[0] ? 1 : -1,
						bounds: {
							x1: Math.min(e, s),
							y1: h,
							x2: Math.max(e, s),
							y2: y
						},
						color: p
					})))
			}
			return v.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: v,
				dest: this.plotArea.ctx,
				animationCallback: r.fadeInAnimation,
				easingFunction: r.easing.easeInQuad,
				animationBase: 0
			}
		}
	};
	t.prototype.renderRangeArea = function(t) {
		function ft() {
			var n, t;
			if (ut) {
				for (n = null, s.lineThickness > 0 && i.stroke(), t = v.length - 1; t >= 0; t--) n = v[t], i.lineTo(n.x, n.y), h.lineTo(n.x, n.y);
				if (i.closePath(), i.globalAlpha = s.fillOpacity, i.fill(), i.globalAlpha = 1, h.fill(), s.lineThickness > 0) {
					for (i.beginPath(), i.moveTo(n.x, n.y), t = 0; t < v.length; t++) n = v[t], i.lineTo(n.x, n.y);
					i.stroke()
				}
				i.beginPath();
				i.moveTo(o, c);
				h.beginPath();
				h.moveTo(o, c);
				ut = {
					x: o,
					y: c
				};
				v = [];
				v.push({
					x: o,
					y: w
				})
			}
		}
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			ht = t.dataSeriesIndexes.length,
			nt, ot, d, it, g, l, k;
		if (!(ht <= 0)) {
			var h = this._eventManager.ghostCtx,
				ct = t.axisX.lineCoordinates,
				lt = t.axisY.lineCoordinates,
				b = [],
				p = this.plotArea;
			for (i.save(), n && h.save(), i.beginPath(), i.rect(p.x1, p.y1, p.width, p.height), i.clip(), n && (h.beginPath(), h.rect(p.x1, p.y1, p.width, p.height), h.clip()), nt = 0; nt < t.dataSeriesIndexes.length; nt++) {
				var v = [],
					rt = t.dataSeriesIndexes[nt],
					s = this.data[rt],
					e = s.dataPoints,
					et = s.id;
				this._eventManager.objectMap[et] = {
					objectType: "dataSeries",
					dataSeriesIndex: rt
				};
				ot = u(et);
				h.fillStyle = ot;
				b = [];
				var st = !0,
					f = 0,
					o, c, w, tt, at = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) + .5 << 0,
					ut = null;
				if (e.length > 0) {
					for (d = s._colorSet[f % s._colorSet.length], i.fillStyle = d, i.strokeStyle = d, i.lineWidth = s.lineThickness, i.setLineDash && i.setLineDash(y(s.lineDashType, s.lineThickness)), it = !0; f < e.length; f++)
						if (tt = e[f].x.getTime ? e[f].x.getTime() : e[f].x, !(tt < t.axisX.dataInfo.viewPortMin) && !(tt > t.axisX.dataInfo.viewPortMax)) {
							if (e[f].y === null || !e[f].y.length || typeof e[f].y[0] != "number" || typeof e[f].y[1] != "number") {
								ft();
								it = !0;
								continue
							}
							o = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (tt - t.axisX.conversionParameters.minimum) + .5 << 0;
							c = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (e[f].y[0] - t.axisY.conversionParameters.minimum) + .5 << 0;
							w = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (e[f].y[1] - t.axisY.conversionParameters.minimum) + .5 << 0;
							st || it ? (i.beginPath(), i.moveTo(o, c), ut = {
								x: o,
								y: c
							}, v = [], v.push({
								x: o,
								y: w
							}), n && (h.beginPath(), h.moveTo(o, c)), st = !1, it = !1) : (i.lineTo(o, c), v.push({
								x: o,
								y: w
							}), n && h.lineTo(o, c), f % 250 == 0 && ft());
							g = s.dataPointIds[f];
							this._eventManager.objectMap[g] = {
								id: g,
								objectType: "dataPoint",
								dataSeriesIndex: rt,
								dataPointIndex: f,
								x1: o,
								y1: c,
								y2: w
							};
							e[f].markerSize !== 0 && (e[f].markerSize > 0 || s.markerSize > 0) && (l = s.getMarkerProperties(f, o, w, i), b.push(l), k = u(g), n && b.push({
								x: o,
								y: w,
								ctx: h,
								type: l.type,
								size: l.size,
								color: k,
								borderColor: k,
								borderThickness: l.borderThickness
							}), l = s.getMarkerProperties(f, o, c, i), b.push(l), k = u(g), n && b.push({
								x: o,
								y: c,
								ctx: h,
								type: l.type,
								size: l.size,
								color: k,
								borderColor: k,
								borderThickness: l.borderThickness
							}));
							(e[f].indexLabel || s.indexLabel || e[f].indexLabelFormatter || s.indexLabelFormatter) && (this._indexLabels.push({
								chartType: "rangeArea",
								dataPoint: e[f],
								dataSeries: s,
								indexKeyword: 0,
								point: {
									x: o,
									y: c
								},
								direction: e[f].y[0] <= e[f].y[1] ? -1 : 1,
								color: d
							}), this._indexLabels.push({
								chartType: "rangeArea",
								dataPoint: e[f],
								dataSeries: s,
								indexKeyword: 1,
								point: {
									x: o,
									y: w
								},
								direction: e[f].y[0] <= e[f].y[1] ? 1 : -1,
								color: d
							}))
						} ft();
					a.drawMarkers(b)
				}
			}
			return i.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	t.prototype.renderRangeSplineArea = function(t) {
		function ft() {
			var t = kt(tt, 2),
				r;
			if (t.length > 0) {
				for (i.beginPath(), i.moveTo(t[0].x, t[0].y), n && (s.beginPath(), s.moveTo(t[0].x, t[0].y)), r = 0; r < t.length - 3; r += 3) i.bezierCurveTo(t[r + 1].x, t[r + 1].y, t[r + 2].x, t[r + 2].y, t[r + 3].x, t[r + 3].y), n && s.bezierCurveTo(t[r + 1].x, t[r + 1].y, t[r + 2].x, t[r + 2].y, t[r + 3].x, t[r + 3].y);
				for (o.lineThickness > 0 && i.stroke(), t = kt(h, 2), i.lineTo(h[h.length - 1].x, h[h.length - 1].y), r = t.length - 1; r > 2; r -= 3) i.bezierCurveTo(t[r - 1].x, t[r - 1].y, t[r - 2].x, t[r - 2].y, t[r - 3].x, t[r - 3].y), n && s.bezierCurveTo(t[r - 1].x, t[r - 1].y, t[r - 2].x, t[r - 2].y, t[r - 3].x, t[r - 3].y);
				if (i.closePath(), i.globalAlpha = o.fillOpacity, i.fill(), i.globalAlpha = 1, o.lineThickness > 0) {
					for (i.beginPath(), i.moveTo(h[h.length - 1].x, h[h.length - 1].y), r = t.length - 1; r > 2; r -= 3) i.bezierCurveTo(t[r - 1].x, t[r - 1].y, t[r - 2].x, t[r - 2].y, t[r - 3].x, t[r - 3].y), n && s.bezierCurveTo(t[r - 1].x, t[r - 1].y, t[r - 2].x, t[r - 2].y, t[r - 3].x, t[r - 3].y);
					i.stroke()
				}
				i.beginPath();
				n && (s.closePath(), s.fill())
			}
		}
		var i = t.targetCanvasCtx || this.plotArea.ctx,
			et = t.dataSeriesIndexes.length,
			g, ut, d, l, k;
		if (!(et <= 0)) {
			var s = this._eventManager.ghostCtx,
				ot = t.axisX.lineCoordinates,
				st = t.axisY.lineCoordinates,
				p = [],
				v = this.plotArea;
			for (i.save(), n && s.save(), i.beginPath(), i.rect(v.x1, v.y1, v.width, v.height), i.clip(), n && (s.beginPath(), s.rect(v.x1, v.y1, v.width, v.height), s.clip()), g = 0; g < t.dataSeriesIndexes.length; g++) {
				var it = t.dataSeriesIndexes[g],
					o = this.data[it],
					e = o.dataPoints,
					rt = o.id;
				this._eventManager.objectMap[rt] = {
					objectType: "dataSeries",
					dataSeriesIndex: it
				};
				ut = u(rt);
				s.fillStyle = ut;
				p = [];
				var f = 0,
					c, w, b, nt, ht = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (0 - t.axisY.conversionParameters.minimum) + .5 << 0,
					tt = [],
					h = [];
				if (e.length > 0) {
					for (color = o._colorSet[f % o._colorSet.length], i.fillStyle = color, i.strokeStyle = color, i.lineWidth = o.lineThickness, i.setLineDash && i.setLineDash(y(o.lineDashType, o.lineThickness)); f < e.length; f++)
						if (nt = e[f].x.getTime ? e[f].x.getTime() : e[f].x, !(nt < t.axisX.dataInfo.viewPortMin) && !(nt > t.axisX.dataInfo.viewPortMax)) {
							if (e[f].y === null || !e[f].y.length || typeof e[f].y[0] != "number" || typeof e[f].y[1] != "number") {
								f > 0 && (ft(), tt = [], h = []);
								continue
							}
							c = t.axisX.conversionParameters.reference + t.axisX.conversionParameters.pixelPerUnit * (nt - t.axisX.conversionParameters.minimum) + .5 << 0;
							w = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (e[f].y[0] - t.axisY.conversionParameters.minimum) + .5 << 0;
							b = t.axisY.conversionParameters.reference + t.axisY.conversionParameters.pixelPerUnit * (e[f].y[1] - t.axisY.conversionParameters.minimum) + .5 << 0;
							d = o.dataPointIds[f];
							this._eventManager.objectMap[d] = {
								id: d,
								objectType: "dataPoint",
								dataSeriesIndex: it,
								dataPointIndex: f,
								x1: c,
								y1: w,
								y2: b
							};
							tt[tt.length] = {
								x: c,
								y: w
							};
							h[h.length] = {
								x: c,
								y: b
							};
							e[f].markerSize !== 0 && (e[f].markerSize > 0 || o.markerSize > 0) && (l = o.getMarkerProperties(f, c, w, i), p.push(l), k = u(d), n && p.push({
								x: c,
								y: w,
								ctx: s,
								type: l.type,
								size: l.size,
								color: k,
								borderColor: k,
								borderThickness: l.borderThickness
							}), l = o.getMarkerProperties(f, c, b, i), p.push(l), k = u(d), n && p.push({
								x: c,
								y: b,
								ctx: s,
								type: l.type,
								size: l.size,
								color: k,
								borderColor: k,
								borderThickness: l.borderThickness
							}));
							(e[f].indexLabel || o.indexLabel || e[f].indexLabelFormatter || o.indexLabelFormatter) && (this._indexLabels.push({
								chartType: "splineArea",
								dataPoint: e[f],
								dataSeries: o,
								indexKeyword: 0,
								point: {
									x: c,
									y: w
								},
								direction: e[f].y[0] <= e[f].y[1] ? -1 : 1,
								color: color
							}), this._indexLabels.push({
								chartType: "splineArea",
								dataPoint: e[f],
								dataSeries: o,
								indexKeyword: 1,
								point: {
									x: c,
									y: b
								},
								direction: e[f].y[0] <= e[f].y[1] ? 1 : -1,
								color: color
							}))
						} ft();
					a.drawMarkers(p)
				}
			}
			return i.restore(), n && this._eventManager.ghostCtx.restore(), {
				source: i,
				dest: this.plotArea.ctx,
				animationCallback: r.xClipAnimation,
				easingFunction: r.easing.linear,
				animationBase: 0
			}
		}
	};
	dt = function(t, i, r, u, f, e, o, s) {
		var h, c, l;
		typeof s == "undefined" && (s = 1);
		n || (h = Number((o % (2 * Math.PI)).toFixed(8)), c = Number((e % (2 * Math.PI)).toFixed(8)), c === h && (o -= .0001));
		t.save();
		t.globalAlpha = s;
		f === "pie" ? (t.beginPath(), t.moveTo(i.x, i.y), t.arc(i.x, i.y, r, e, o, !1), t.fillStyle = u, t.strokeStyle = "white", t.lineWidth = 2, t.closePath(), t.fill()) : f === "doughnut" && (l = .6, t.beginPath(), t.arc(i.x, i.y, r, e, o, !1), t.arc(i.x, i.y, l * r, o, e, !0), t.closePath(), t.fillStyle = u, t.strokeStyle = "white", t.lineWidth = 2, t.fill());
		t.globalAlpha = 1;
		t.restore()
	};
	t.prototype.renderPie = function(n) {
		function et() {
			var w, b, tt, a, n;
			if (i && e) {
				var y = 0,
					p = 0,
					d = 0,
					g = 0;
				for (o = 0; o < e.length; o++) {
					var h = e[o],
						nt = i.dataPointIds[o],
						n = {
							id: nt,
							objectType: "dataPoint",
							dataPointIndex: o,
							dataSeriesIndex: 0
						};
					t.push(n);
					w = {
						percent: null,
						total: null
					};
					b = null;
					w = r.getPercentAndTotal(i, h);
					(i.indexLabelFormatter || h.indexLabelFormatter) && (b = {
						chart: r._options,
						dataSeries: i,
						dataPoint: h,
						total: w.total,
						percent: w.percent
					});
					tt = h.indexLabelFormatter ? h.indexLabelFormatter(b) : h.indexLabel ? r.replaceKeywordsWithValue(h.indexLabel, h, i, o) : i.indexLabelFormatter ? i.indexLabelFormatter(b) : i.indexLabel ? r.replaceKeywordsWithValue(i.indexLabel, h, i, o) : h.label ? h.label : "";
					r._eventManager.objectMap[nt] = n;
					n.center = {
						x: u.x,
						y: u.y
					};
					n.y = h.y;
					n.radius = s;
					n.indexLabelText = tt;
					n.indexLabelPlacement = i.indexLabelPlacement;
					n.indexLabelLineColor = h.indexLabelLineColor ? h.indexLabelLineColor : i.indexLabelLineColor ? i.indexLabelLineColor : h.color ? h.color : i._colorSet[o % i._colorSet.length];
					n.indexLabelLineThickness = h.indexLabelLineThickness ? h.indexLabelLineThickness : i.indexLabelLineThickness;
					n.indexLabelLineDashType = h.indexLabelLineDashType ? h.indexLabelLineDashType : i.indexLabelLineDashType;
					n.indexLabelFontColor = h.indexLabelFontColor ? h.indexLabelFontColor : i.indexLabelFontColor;
					n.indexLabelFontStyle = h.indexLabelFontStyle ? h.indexLabelFontStyle : i.indexLabelFontStyle;
					n.indexLabelFontWeight = h.indexLabelFontWeight ? h.indexLabelFontWeight : i.indexLabelFontWeight;
					n.indexLabelFontSize = h.indexLabelFontSize ? h.indexLabelFontSize : i.indexLabelFontSize;
					n.indexLabelFontFamily = h.indexLabelFontFamily ? h.indexLabelFontFamily : i.indexLabelFontFamily;
					n.indexLabelBackgroundColor = h.indexLabelBackgroundColor ? h.indexLabelBackgroundColor : i.indexLabelBackgroundColor ? i.indexLabelBackgroundColor : null;
					n.indexLabelMaxWidth = h.indexLabelMaxWidth ? h.indexLabelMaxWidth : i.indexLabelMaxWidth ? i.indexLabelMaxWidth : f.width * .33;
					n.indexLabelWrap = typeof h.indexLabelWrap != "undefined" ? h.indexLabelWrap : i.indexLabelWrap;
					n.startAngle = o === 0 ? i.startAngle ? i.startAngle / 180 * Math.PI : 0 : t[o - 1].endAngle;
					n.startAngle = (n.startAngle + 2 * Math.PI) % (2 * Math.PI);
					n.endAngle = n.startAngle + 2 * Math.PI / k * Math.abs(h.y);
					a = (n.endAngle + n.startAngle) / 2;
					a = (a + 2 * Math.PI) % (2 * Math.PI);
					n.midAngle = a;
					n.midAngle > Math.PI / 2 - l && n.midAngle < Math.PI / 2 + l ? ((y === 0 || t[d].midAngle > n.midAngle) && (d = o), y++) : n.midAngle > 3 * Math.PI / 2 - l && n.midAngle < 3 * Math.PI / 2 + l && ((p === 0 || t[g].midAngle > n.midAngle) && (g = o), p++);
					n.hemisphere = a > Math.PI / 2 && a <= 3 * Math.PI / 2 ? "left" : "right";
					n.indexLabelTextBlock = new c(r.plotArea.ctx, {
						fontSize: n.indexLabelFontSize,
						fontFamily: n.indexLabelFontFamily,
						fontColor: n.indexLabelFontColor,
						fontStyle: n.indexLabelFontStyle,
						fontWeight: n.indexLabelFontWeight,
						horizontalAlign: "left",
						backgroundColor: n.indexLabelBackgroundColor,
						maxWidth: n.indexLabelMaxWidth,
						maxHeight: n.indexLabelWrap ? n.indexLabelFontSize * 5 : n.indexLabelFontSize * 1.5,
						text: n.indexLabelText,
						padding: 0,
						textBaseline: "top"
					});
					n.indexLabelTextBlock.measureText()
				}
				var it = 0,
					rt = 0,
					v = !1;
				for (o = 0; o < e.length; o++) n = t[(d + o) % e.length], y > 1 && n.midAngle > Math.PI / 2 - l && n.midAngle < Math.PI / 2 + l && (it <= y / 2 && !v ? (n.hemisphere = "right", it++) : (n.hemisphere = "left", v = !0));
				for (v = !1, o = 0; o < e.length; o++) n = t[(g + o) % e.length], p > 1 && n.midAngle > 3 * Math.PI / 2 - l && n.midAngle < 3 * Math.PI / 2 + l && (rt <= p / 2 && !v ? (n.hemisphere = "left", rt++) : (n.hemisphere = "right", v = !0))
			}
		}

		function ot() {
			var u = r.plotArea.ctx,
				l, f, a, n, o, h, c;
			for (u.fillStyle = "black", u.strokeStyle = "grey", l = 16, u.textBaseline = "middle", u.lineJoin = "round", f = 0, a = 0, f = 0; f < e.length; f++)(n = t[f], n.indexLabelText) && (n.indexLabelTextBlock.y -= n.indexLabelTextBlock.height / 2, o = 0, o = n.hemisphere === "left" ? i.indexLabelPlacement !== "inside" ? -(n.indexLabelTextBlock.width + v) : -n.indexLabelTextBlock.width / 2 : i.indexLabelPlacement !== "inside" ? v : -n.indexLabelTextBlock.width / 2, n.indexLabelTextBlock.x += o, n.indexLabelTextBlock.render(!0), n.indexLabelTextBlock.x -= o, n.indexLabelTextBlock.y += n.indexLabelTextBlock.height / 2, n.indexLabelPlacement !== "inside" && (h = n.center.x + s * Math.cos(n.midAngle), c = n.center.y + s * Math.sin(n.midAngle), u.strokeStyle = n.indexLabelLineColor, u.lineWidth = n.indexLabelLineThickness, u.setLineDash && u.setLineDash(y(n.indexLabelLineDashType, n.indexLabelLineThickness)), u.beginPath(), u.moveTo(h, c), u.lineTo(n.indexLabelTextBlock.x, n.indexLabelTextBlock.y), u.lineTo(n.indexLabelTextBlock.x + (n.hemisphere === "left" ? -v : v), n.indexLabelTextBlock.y), u.stroke()), u.lineJoin = "miter")
		}

		function st(n) {
			var s = r.plotArea.ctx,
				h, u, a;
			for (s.clearRect(f.x1, f.y1, f.width, f.height), s.fillStyle = r.backgroundColor, s.fillRect(f.x1, f.y1, f.width, f.height), h = t[0].startAngle + 2 * Math.PI * n, u = 0; u < e.length; u++) {
				var c = u === 0 ? t[u].startAngle : o,
					o = c + (t[u].endAngle - t[u].startAngle),
					l = !1;
				if (o > h && (o = h, l = !0), a = e[u].color ? e[u].color : i._colorSet[u % i._colorSet.length], o > c && dt(r.plotArea.ctx, t[u].center, t[u].radius, a, i.type, c, o, i.fillOpacity), l) break
			}
		}

		function rt(n) {
			var c = r.plotArea.ctx,
				o, l, a, h, w;
			for (c.clearRect(f.x1, f.y1, f.width, f.height), c.fillStyle = r.backgroundColor, c.fillRect(f.x1, f.y1, f.width, f.height), o = 0; o < e.length; o++)
				if (l = t[o].startAngle, a = t[o].endAngle, a > l) {
					var v = s * .07 * Math.cos(t[o].midAngle),
						y = s * .07 * Math.sin(t[o].midAngle),
						p = !1;
					e[o].exploded ? (Math.abs(t[o].center.x - (u.x + v)) > 1e-9 || Math.abs(t[o].center.y - (u.y + y)) > 1e-9) && (t[o].center.x = u.x + v * n, t[o].center.y = u.y + y * n, p = !0) : (Math.abs(t[o].center.x - u.x) > 0 || Math.abs(t[o].center.y - u.y) > 0) && (t[o].center.x = u.x + v * (1 - n), t[o].center.y = u.y + y * (1 - n), p = !0);
					p && (h = {}, h.dataSeries = i, h.dataPoint = i.dataPoints[o], h.index = o, r._toolTip.highlightObjects([h]));
					w = e[o].color ? e[o].color : i._colorSet[o % i._colorSet.length];
					dt(r.plotArea.ctx, t[o].center, t[o].radius, w, i.type, l, a, i.fillOpacity)
				} ot()
		}

		function ht(n, t) {
			var i = {
					x1: n.indexLabelTextBlock.x,
					y1: n.indexLabelTextBlock.y - n.indexLabelTextBlock.height / 2,
					x2: n.indexLabelTextBlock.x + n.indexLabelTextBlock.width,
					y2: n.indexLabelTextBlock.y + n.indexLabelTextBlock.height / 2
				},
				r = {
					x1: t.indexLabelTextBlock.x,
					y1: t.indexLabelTextBlock.y - t.indexLabelTextBlock.height / 2,
					x2: t.indexLabelTextBlock.x + t.indexLabelTextBlock.width,
					y2: t.indexLabelTextBlock.y + t.indexLabelTextBlock.height / 2
				};
			return i.x2 < r.x1 - v || i.x1 > r.x2 + v || i.y1 > r.y2 + v || i.y2 < r.y1 - v ? !1 : !0
		}

		function b(n, t) {
			var i = {
					y: n.indexLabelTextBlock.y,
					y1: n.indexLabelTextBlock.y - n.indexLabelTextBlock.height / 2,
					y2: n.indexLabelTextBlock.y + n.indexLabelTextBlock.height / 2
				},
				r = {
					y: t.indexLabelTextBlock.y,
					y1: t.indexLabelTextBlock.y - t.indexLabelTextBlock.height / 2,
					y2: t.indexLabelTextBlock.y + t.indexLabelTextBlock.height / 2
				};
			return r.y > i.y ? r.y1 - i.y2 : i.y1 - r.y2
		}

		function d(n) {
			for (var i = null, r = 1; r < e.length; r++)
				if (i = (n + r + t.length) % t.length, t[i].hemisphere !== t[n].hemisphere) {
					i = null;
					break
				} else if (t[i].indexLabelText && i !== n && (b(t[i], t[n]) < 0 || (t[n].hemisphere === "right" ? t[i].indexLabelTextBlock.y >= t[n].indexLabelTextBlock.y : t[i].indexLabelTextBlock.y <= t[n].indexLabelTextBlock.y))) break;
			else i = null;
			return i
		}

		function ct(n) {
			for (var i = null, r = 1; r < e.length; r++)
				if (i = (n - r + t.length) % t.length, t[i].hemisphere !== t[n].hemisphere) {
					i = null;
					break
				} else if (t[i].indexLabelText && t[i].hemisphere === t[n].hemisphere && i !== n && (b(t[i], t[n]) < 0 || (t[n].hemisphere === "right" ? t[i].indexLabelTextBlock.y <= t[n].indexLabelTextBlock.y : t[i].indexLabelTextBlock.y >= t[n].indexLabelTextBlock.y))) break;
			else i = null;
			return i
		}

		function p(n, i) {
			var r, ut, st, b, v, ft, o;
			i = i || 0;
			var k = 0,
				rt = u.y - h * 1,
				et = u.y + h * 1;
			if (n >= 0 && n < e.length) {
				if (r = t[n], i < 0 && r.indexLabelTextBlock.y < rt || i > 0 && r.indexLabelTextBlock.y > et) return 0;
				var f = i,
					ot = 0,
					ht = 0,
					lt = 0,
					at = 0,
					vt = 0;
				f < 0 ? r.indexLabelTextBlock.y - r.indexLabelTextBlock.height / 2 > rt && r.indexLabelTextBlock.y - r.indexLabelTextBlock.height / 2 + f < rt && (f = -(rt - (r.indexLabelTextBlock.y - r.indexLabelTextBlock.height / 2 + f))) : r.indexLabelTextBlock.y + r.indexLabelTextBlock.height / 2 < rt && r.indexLabelTextBlock.y + r.indexLabelTextBlock.height / 2 + f > et && (f = r.indexLabelTextBlock.y + r.indexLabelTextBlock.height / 2 + f - et);
				ut = r.indexLabelTextBlock.y + f;
				st = 0;
				st = r.hemisphere === "right" ? u.x + Math.sqrt(Math.pow(h, 2) - Math.pow(ut - u.y, 2)) : u.x - Math.sqrt(Math.pow(h, 2) - Math.pow(ut - u.y, 2));
				ht = u.x + s * Math.cos(r.midAngle);
				lt = u.y + s * Math.sin(r.midAngle);
				ot = Math.sqrt(Math.pow(st - ht, 2) + Math.pow(ut - lt, 2));
				vt = Math.acos(s / h);
				at = Math.acos((h * h + s * s - ot * ot) / (2 * s * h));
				f = at < vt ? ut - r.indexLabelTextBlock.y : 0;
				var yt = ct(n),
					pt = d(n),
					c, v, y = 0,
					g = 0;
				if (f < 0 ? (c = r.hemisphere === "right" ? yt : pt, k = f, c !== null && (b = -f, v = r.indexLabelTextBlock.y - r.indexLabelTextBlock.height / 2 - (t[c].indexLabelTextBlock.y + t[c].indexLabelTextBlock.height / 2), v - b < w && (y = -b, tt++, g = p(c, y), +g.toFixed(a) > +y.toFixed(a) && (k = v > w ? -(v - w) : -(b - (g - y)))))) : f > 0 && (c = r.hemisphere === "right" ? pt : yt, k = f, c !== null && (b = f, v = t[c].indexLabelTextBlock.y - t[c].indexLabelTextBlock.height / 2 - (r.indexLabelTextBlock.y + r.indexLabelTextBlock.height / 2), v - b < w && (y = b, tt++, g = p(c, y), +g.toFixed(a) < +y.toFixed(a) && (k = v > w ? v - w : b - (y - g))))), k) {
					if (ft = r.indexLabelTextBlock.y + k, o = 0, o = r.hemisphere === "right" ? u.x + Math.sqrt(Math.pow(h, 2) - Math.pow(ft - u.y, 2)) : u.x - Math.sqrt(Math.pow(h, 2) - Math.pow(ft - u.y, 2)), r.midAngle > Math.PI / 2 - l && r.midAngle < Math.PI / 2 + l) {
						var wt = (n - 1 + t.length) % t.length,
							nt = t[wt],
							it = t[(n + 1 + t.length) % t.length];
						r.hemisphere === "left" && nt.hemisphere === "right" && o > nt.indexLabelTextBlock.x ? o = nt.indexLabelTextBlock.x - 15 : r.hemisphere === "right" && it.hemisphere === "left" && o < it.indexLabelTextBlock.x && (o = it.indexLabelTextBlock.x + 15)
					} else if (r.midAngle > 3 * Math.PI / 2 - l && r.midAngle < 3 * Math.PI / 2 + l) {
						var wt = (n - 1 + t.length) % t.length,
							nt = t[wt],
							it = t[(n + 1 + t.length) % t.length];
						r.hemisphere === "right" && nt.hemisphere === "left" && o < nt.indexLabelTextBlock.x ? o = nt.indexLabelTextBlock.x + 15 : r.hemisphere === "left" && it.hemisphere === "right" && o > it.indexLabelTextBlock.x && (o = it.indexLabelTextBlock.x - 15)
					}
					r.indexLabelTextBlock.y = ft;
					r.indexLabelTextBlock.x = o;
					r.indexLabelAngle = Math.atan2(r.indexLabelTextBlock.y - u.y, r.indexLabelTextBlock.x - u.x)
				}
			}
			return k
		}

		function lt() {
			var yt = r.plotArea.ctx,
				gt, tt, rt, l, kt, wt, n, ni, ti, dt, et, g, ot, nt, c, vt, lt;
			yt.fillStyle = "grey";
			yt.strokeStyle = "grey";
			gt = 16;
			yt.font = gt + "px Arial";
			yt.textBaseline = "middle";
			for (var o = 0, pt = 0, k = 0, pt = 0; pt < 10 && (pt < 1 || k > 0); pt++) {
				if (s -= k, k = 0, i.indexLabelPlacement !== "inside") {
					for (h = s * it, o = 0; o < e.length; o++) n = t[o], n.indexLabelTextBlock.x = u.x + h * Math.cos(n.midAngle), n.indexLabelTextBlock.y = u.y + h * Math.sin(n.midAngle), n.indexLabelAngle = n.midAngle, n.radius = s;
					for (o = 0; o < e.length; o++)
						if ((n = t[o], rt = d(o), rt !== null) && (tt = t[o], lt = t[rt], l = 0, l = b(tt, lt) - w, l < 0)) {
							for (kt = 0, wt = 0, c = 0; c < e.length; c++) c !== o && t[c].hemisphere === n.hemisphere && (t[c].indexLabelTextBlock.y < n.indexLabelTextBlock.y ? kt++ : wt++);
							var at = l / (kt + wt || 1) * wt,
								y = -1 * (l - at),
								ut = 0,
								ft = 0;
							n.hemisphere === "right" ? (ut = p(o, at), y = -1 * (l - ut), ft = p(rt, y), +ft.toFixed(a) < +y.toFixed(a) && +ut.toFixed(a) <= +at.toFixed(a) && p(o, -(y - ft))) : (ut = p(rt, at), y = -1 * (l - ut), ft = p(o, y), +ft.toFixed(a) < +y.toFixed(a) && +ut.toFixed(a) <= +at.toFixed(a) && p(rt, -(y - ft)))
						}
				} else
					for (o = 0; o < e.length; o++) n = t[o], h = i.type === "pie" ? s * .7 : s * .8, ni = u.x + h * Math.cos(n.midAngle), ti = u.y + h * Math.sin(n.midAngle), n.indexLabelTextBlock.x = ni, n.indexLabelTextBlock.y = ti;
				for (o = 0; o < e.length; o++)(n = t[o], dt = n.indexLabelTextBlock.measureText(), dt.height !== 0 && dt.width !== 0) && (et = 0, g = 0, et = n.hemisphere === "right" ? (f.x2 - (n.indexLabelTextBlock.x + n.indexLabelTextBlock.width + v)) * -1 : f.x1 - (n.indexLabelTextBlock.x - n.indexLabelTextBlock.width - v), et > 0 && (Math.abs(n.indexLabelTextBlock.y - n.indexLabelTextBlock.height / 2 - u.y) < s || Math.abs(n.indexLabelTextBlock.y + n.indexLabelTextBlock.height / 2 - u.y) < s) && (g = et / Math.abs(Math.cos(n.indexLabelAngle)), g > 9 && (g = g * .3), g > k && (k = g)), ot = 0, nt = 0, ot = n.indexLabelAngle > 0 && n.indexLabelAngle < Math.PI ? (f.y2 - (n.indexLabelTextBlock.y + n.indexLabelTextBlock.height / 2 + 5)) * -1 : f.y1 - (n.indexLabelTextBlock.y - n.indexLabelTextBlock.height / 2 - 5), ot > 0 && Math.abs(n.indexLabelTextBlock.x - u.x) < s && (nt = ot / Math.abs(Math.sin(n.indexLabelAngle)), nt > 9 && (nt = nt * .3), nt > k && (k = nt)));

				function r(n, i, r) {
					for (var f, o = [], s = 0, u = i;; u = (u + 1 + e.length) % e.length)
						if (o.push(t[u]), u === r) break;
					for (o.sort(function(n, t) {
							return n.y - t.y
						}), u = 0; u < o.length; u++)
						if (f = o[u], s < n) s += f.indexLabelTextBlock.height, f.indexLabelTextBlock.text = "", f.indexLabelText = "", f.indexLabelTextBlock.measureText();
						else break
				}
				var st = -1,
					bt = -1,
					ct = 0;
				for (c = 0; c < e.length; c++)(tt = t[c], tt.indexLabelText) && (vt = d(c), vt !== null) && (lt = t[vt], l = 0, l = b(tt, lt), l < 0 && ht(tt, lt) ? (st < 0 && (st = c), vt !== st && (bt = vt), ct += -l) : ct > 0 && (r(ct, st, bt), st = -1, bt = -1, ct = 0));
				ct > 0 && r(ct, st, bt)
			}
		}

		function g() {
			var t, n;
			if (r.plotArea.layoutManager.reset(), r._title && (r._title.dockInsidePlotArea || r._title.horizontalAlign === "center" && r._title.verticalAlign === "center") && r._title.render(), r.subtitles)
				for (t = 0; t < r.subtitles.length; t++) n = r.subtitles[t], (n.dockInsidePlotArea || n.horizontalAlign === "center" && n.verticalAlign === "center") && n.render();
			r.legend && (r.legend.dockInsidePlotArea || r.legend.horizontalAlign === "center" && r.legend.verticalAlign === "center") && r.legend.render()
		}
		var r = this,
			ut = n.dataSeriesIndexes.length,
			o;
		if (!(ut <= 0)) {
			var ft = n.dataSeriesIndexes[0],
				i = this.data[ft],
				e = i.dataPoints,
				v = 10,
				nt = 500,
				f = this.plotArea,
				tt = 0,
				t = [],
				w = 2,
				it = 1.3,
				l = 20 / 180 * Math.PI,
				a = 6,
				u = {
					x: (f.x2 + f.x1) / 2,
					y: (f.y2 + f.y1) / 2
				},
				s = i.indexLabelPlacement === "inside" ? Math.min(f.width, f.height) * .92 / 2 : Math.min(f.width, f.height) * .8 / 2,
				at = s * .6,
				h = s * it,
				vt = s,
				k = 0;
			for (o = 0; o < e.length; o++) k += Math.abs(e[o].y);
			k !== 0 && (this.pieDoughnutClickHandler = function(n) {
				if (!r.isAnimating) {
					var u = n.dataPointIndex,
						t = n.dataPoint,
						i = this,
						f = i.dataPointIds[u];
					t.exploded = t.exploded ? !1 : !0;
					i.dataPoints.length > 1 && r._animator.animate(0, nt, function(n) {
						rt(n);
						g()
					});
					return
				}
			}, et(), lt(), this.disableToolTip = !0, this._animator.animate(0, this.animatedRender ? this.animationDuration : 0, function(n) {
				st(n);
				g()
			}, function() {
				r.disableToolTip = !1;
				r._animator.animate(0, r.animatedRender ? nt : 0, function(n) {
					rt(n);
					g()
				})
			}))
		}
	};
	t.prototype.animationRequestId = null;
	t.prototype.requestAnimFrame = function() {
		return window.requestAnimationFrame || window.webkitRequestAnimationFrame || window.mozRequestAnimationFrame || window.oRequestAnimationFrame || window.msRequestAnimationFrame || function(n) {
			window.setTimeout(n, 1e3 / 60)
		}
	}();
	t.prototype.cancelRequestAnimFrame = function() {
		return window.cancelAnimationFrame || window.webkitCancelRequestAnimationFrame || window.mozCancelRequestAnimationFrame || window.oCancelRequestAnimationFrame || window.msCancelRequestAnimationFrame || clearTimeout
	}();
	ft.prototype.registerSpace = function(n, t) {
		n === "top" ? this._topOccupied += t.height : n === "bottom" ? this._bottomOccupied += t.height : n === "left" ? this._leftOccupied += t.width : n === "right" && (this._rightOccupied += t.width)
	};
	ft.prototype.unRegisterSpace = function(n, t) {
		n === "top" ? this._topOccupied -= t.height : n === "bottom" ? this._bottomOccupied -= t.height : n === "left" ? this._leftOccupied -= t.width : n === "right" && (this._rightOccupied -= t.width)
	};
	ft.prototype.getFreeSpace = function() {
		return {
			x1: this._x1 + this._leftOccupied,
			y1: this._y1 + this._topOccupied,
			x2: this._x2 - this._rightOccupied,
			y2: this._y2 - this._bottomOccupied,
			width: this._x2 - this._x1 - this._rightOccupied - this._leftOccupied,
			height: this._y2 - this._y1 - this._bottomOccupied - this._topOccupied
		}
	};
	ft.prototype.reset = function() {
		this._topOccupied = this._padding;
		this._bottomOccupied = this._padding;
		this._leftOccupied = this._padding;
		this._rightOccupied = this._padding
	};
	w(c, h);
	c.prototype.render = function(n) {
		var f, i, u;
		n && this.ctx.save();
		f = this.ctx.font;
		this.ctx.textBaseline = this.textBaseline;
		i = 0;
		this._isDirty && this.measureText(this.ctx);
		this.ctx.translate(this.x, this.y + i);
		this.textBaseline === "middle" && (i = -this._lineHeight / 2);
		this.ctx.font = this._getFontString();
		this.ctx.rotate(Math.PI / 180 * this.angle);
		var r = 0,
			e = this.padding,
			t = null;
		for ((this.borderThickness > 0 && this.borderColor || this.backgroundColor) && this.ctx.roundRect(0, i, this.width, this.height, this.cornerRadius, this.borderThickness, this.backgroundColor, this.borderColor), this.ctx.fillStyle = this.fontColor, u = 0; u < this._wrappedText.lines.length; u++) t = this._wrappedText.lines[u], this.horizontalAlign === "right" ? r = this.width - t.width - this.padding : this.horizontalAlign === "left" ? r = this.padding : this.horizontalAlign === "center" && (r = (this.width - this.padding * 2) / 2 - t.width / 2 + this.padding), this.ctx.fillText(t.text, r, e), e += t.height;
		this.ctx.font = f;
		n && this.ctx.restore()
	};
	c.prototype.setText = function(n) {
		this.text = n;
		this._isDirty = !0;
		this._wrappedText = null
	};
	c.prototype.measureText = function() {
		if (this.maxWidth === null) throw "Please set maxWidth and height for TextBlock";
		return this._wrapText(this.ctx), this._isDirty = !1, {
			width: this.width,
			height: this.height
		}
	};
	c.prototype._getLineWithWidth = function(n, t, i) {
		var r, h, e;
		if (n = String(n), i = i || !1, !n) return {
			text: "",
			width: 0
		};
		var u = 0,
			o = 0,
			s = n.length - 1,
			f = Infinity;
		for (this.ctx.font = this._getFontString(); o <= s;)
			if (f = Math.floor((o + s) / 2), r = n.substr(0, f + 1), u = this.ctx.measureText(r).width, u < t) o = f + 1;
			else if (u > t) s = f - 1;
		else break;
		return u > t && r.length > 1 && (r = r.substr(0, r.length - 1), u = this.ctx.measureText(r).width), h = !0, (r.length === n.length || n[r.length] === " ") && (h = !1), h && (e = r.split(" "), e.length > 1 && e.pop(), r = e.join(" "), u = this.ctx.measureText(r).width), {
			text: r,
			width: u
		}
	};
	c.prototype._wrapText = function() {
		var t = new String(ht(String(this.text))),
			u = [],
			e = this.ctx.font,
			i = 0,
			r = 0,
			n;
		for (this.ctx.font = this._getFontString(); t.length > 0;) {
			var o = this.maxWidth - this.padding * 2,
				f = this.maxHeight - this.padding * 2,
				n = this._getLineWithWidth(t, o, !1);
			n.height = this._lineHeight;
			u.push(n);
			r = Math.max(r, n.width);
			i += n.height;
			t = ht(t.slice(n.text.length, t.length));
			f && i > f && (n = u.pop(), i -= n.height)
		}
		this._wrappedText = {
			lines: u,
			width: r,
			height: i
		};
		this.width = r + this.padding * 2;
		this.height = i + this.padding * 2;
		this.ctx.font = e
	};
	c.prototype._getFontString = function() {
		return gi("", this, null)
	};
	w(lt, h);
	lt.prototype.render = function() {
		var e, i;
		if (this.text) {
			var o = this.dockInsidePlotArea ? this.chart.plotArea : this.chart,
				n = o.layoutManager.getFreeSpace(),
				u = n.x1,
				f = n.y1,
				h = 0,
				s = 0,
				t = 2,
				a = this.chart._menuButton && this.chart.exportEnabled && this.verticalAlign === "top" ? 22 : 0,
				l, r;
			this.verticalAlign === "top" || this.verticalAlign === "bottom" ? (this.maxWidth === null && (this.maxWidth = n.width - t * 2 - a * (this.horizontalAlign === "center" ? 2 : 1)), s = n.height * .5 - this.margin - t, h = 0) : this.verticalAlign === "center" && (this.horizontalAlign === "left" || this.horizontalAlign === "right" ? (this.maxWidth === null && (this.maxWidth = n.height - t * 2), s = n.width * .5 - this.margin - t) : this.horizontalAlign === "center" && (this.maxWidth === null && (this.maxWidth = n.width - t * 2), s = n.height * .5 - t * 2));
			this.wrap || (s = Math.min(s, Math.max(this.fontSize * 1.5, this.fontSize + this.padding * 2.5)));
			e = new c(this.ctx, {
				fontSize: this.fontSize,
				fontFamily: this.fontFamily,
				fontColor: this.fontColor,
				fontStyle: this.fontStyle,
				fontWeight: this.fontWeight,
				horizontalAlign: this.horizontalAlign,
				verticalAlign: this.verticalAlign,
				borderColor: this.borderColor,
				borderThickness: this.borderThickness,
				backgroundColor: this.backgroundColor,
				maxWidth: this.maxWidth,
				maxHeight: s,
				cornerRadius: this.cornerRadius,
				text: this.text,
				padding: this.padding,
				textBaseline: "top"
			});
			i = e.measureText();
			this.verticalAlign === "top" || this.verticalAlign === "bottom" ? (this.verticalAlign === "top" ? (f = n.y1 + t, r = "top") : this.verticalAlign === "bottom" && (f = n.y2 - t - i.height, r = "bottom"), this.horizontalAlign === "left" ? u = n.x1 + t : this.horizontalAlign === "center" ? u = n.x1 + n.width / 2 - i.width / 2 : this.horizontalAlign === "right" && (u = n.x2 - t - i.width - a), l = this.horizontalAlign, this.width = i.width, this.height = i.height) : this.verticalAlign === "center" && (this.horizontalAlign === "left" ? (u = n.x1 + t, f = n.y2 - t - (this.maxWidth / 2 - i.width / 2), h = -90, r = "left", this.width = i.height, this.height = i.width) : this.horizontalAlign === "right" ? (u = n.x2 - t, f = n.y1 + t + (this.maxWidth / 2 - i.width / 2), h = 90, r = "right", this.width = i.height, this.height = i.width) : this.horizontalAlign === "center" && (f = o.y1 + (o.height / 2 - i.height / 2), u = o.x1 + (o.width / 2 - i.width / 2), r = "center", this.width = i.width, this.height = i.height), l = "center");
			e.x = u;
			e.y = f;
			e.angle = h;
			e.horizontalAlign = l;
			e.render(!0);
			o.layoutManager.registerSpace(r, {
				width: this.width + (r === "left" || r === "right" ? this.margin + t : 0),
				height: this.height + (r === "top" || r === "bottom" ? this.margin + t : 0)
			});
			this.bounds = {
				x1: u,
				y1: f,
				x2: u + this.width,
				y2: f + this.height
			};
			this.ctx.textBaseline = "top"
		}
	};
	w(gt, h);
	gt.prototype.render = lt.prototype.render;
	w(ni, h);
	ni.prototype.render = function() {
		var ot = this.dockInsidePlotArea ? this.chart.plotArea : this.chart,
			f = ot.layoutManager.getFreeSpace(),
			ft = null,
			w = 0,
			b = 0,
			s = 0,
			h = 0,
			y = [],
			k = [],
			t, p, n, it, e, r, v, nt, at;
		for (this.verticalAlign === "top" || this.verticalAlign === "bottom" ? (this.orientation = "horizontal", ft = this.verticalAlign, s = this.maxWidth !== null ? this.maxWidth : f.width * .7, h = this.maxHeight !== null ? this.maxHeight : f.height * .5) : this.verticalAlign === "center" && (this.orientation = "vertical", ft = this.horizontalAlign, s = this.maxWidth !== null ? this.maxWidth : f.width * .5, h = this.maxHeight !== null ? this.maxHeight : f.height * .7), e = 0; e < this.dataSeries.length; e++) {
			if (t = this.dataSeries[e], t.type !== "pie" && t.type !== "doughnut" && t.type !== "funnel") {
				var st = t.legendMarkerType ? t.legendMarkerType : (t.type === "line" || t.type === "stepLine" || t.type === "spline" || t.type === "scatter" || t.type === "bubble") && t.markerType ? t.markerType : d.getDefaultLegendMarker(t.type),
					g = t.legendText ? t.legendText : this.itemTextFormatter ? this.itemTextFormatter({
						chart: this.chart,
						legend: this._options,
						dataSeries: t,
						dataPoint: null
					}) : t.name,
					ht = t.legendMarkerColor ? t.legendMarkerColor : t.markerColor ? t.markerColor : t._colorSet[0],
					o = !t.markerSize && (t.type === "line" || t.type === "stepLine" || t.type === "spline") ? 0 : this.lineHeight * .6,
					ct = t.legendMarkerBorderColor ? t.legendMarkerBorderColor : t.markerBorderColor,
					lt = t.legendMarkerBorderThickness ? t.legendMarkerBorderThickness : t.markerBorderThickness ? Math.max(1, Math.round(o * .2)) : 0,
					vt = t._colorSet[0];
				g = this.chart.replaceKeywordsWithValue(g, t.dataPoints[0], t, e);
				n = {
					markerType: st,
					markerColor: ht,
					text: g,
					textBlock: null,
					chartType: t.type,
					markerSize: o,
					lineColor: t._colorSet[0],
					dataSeriesIndex: t.index,
					dataPointIndex: null,
					markerBorderColor: ct,
					markerBorderThickness: lt
				};
				y.push(n)
			} else
				for (p = 0; p < t.dataPoints.length; p++) {
					var i = t.dataPoints[p],
						st = i.legendMarkerType ? i.legendMarkerType : t.legendMarkerType ? t.legendMarkerType : d.getDefaultLegendMarker(t.type),
						g = i.legendText ? i.legendText : t.legendText ? t.legendText : this.itemTextFormatter ? this.itemTextFormatter({
							chart: this.chart,
							legend: this._options,
							dataSeries: t,
							dataPoint: i
						}) : i.name ? i.name : "DataPoint: " + (p + 1),
						ht = i.legendMarkerColor ? i.legendMarkerColor : t.legendMarkerColor ? t.legendMarkerColor : i.color ? i.color : t.color ? t.color : t._colorSet[p % t._colorSet.length],
						o = this.lineHeight * .6,
						ct = i.legendMarkerBorderColor ? i.legendMarkerBorderColor : t.legendMarkerBorderColor ? t.legendMarkerBorderColor : i.markerBorderColor ? i.markerBorderColor : t.markerBorderColor,
						lt = i.legendMarkerBorderThickness ? i.legendMarkerBorderThickness : t.legendMarkerBorderThickness ? t.legendMarkerBorderThickness : i.markerBorderThickness || t.markerBorderThickness ? Math.max(1, Math.round(o * .2)) : 0;
					g = this.chart.replaceKeywordsWithValue(g, i, t, p);
					n = {
						markerType: st,
						markerColor: ht,
						text: g,
						textBlock: null,
						chartType: t.type,
						markerSize: o,
						dataSeriesIndex: e,
						dataPointIndex: p,
						markerBorderColor: ct,
						markerBorderThickness: lt
					};
					(i.showInLegend || t.showInLegend && i.showInLegend !== !1) && y.push(n)
				}
			n = null
		}
		if (this.reversed === !0 && y.reverse(), y.length > 0) {
			var r = null,
				et = 0,
				l = 0,
				v = 0;
			for (l = this.itemWidth !== null ? this.itemMaxWidth !== null ? Math.min(this.itemWidth, this.itemMaxWidth, s) : Math.min(this.itemWidth, s) : this.itemMaxWidth !== null ? Math.min(this.itemMaxWidth, s) : s, o = o === 0 ? this.lineHeight * .6 : o, l = l - (o + this.horizontalSpacing * .1), e = 0; e < y.length; e++)(n = y[e], (n.chartType === "line" || n.chartType === "spline" || n.chartType === "stepLine") && (l = l - 2 * this.lineHeight * .1), h <= 0 || typeof h == "undefined" || l <= 0 || typeof l == "undefined") || (this.orientation === "horizontal" ? (n.textBlock = new c(this.ctx, {
				x: 0,
				y: 0,
				maxWidth: l,
				maxHeight: this.itemWrap ? h : this.lineHeight,
				angle: 0,
				text: n.text,
				horizontalAlign: "left",
				fontSize: this.fontSize,
				fontFamily: this.fontFamily,
				fontWeight: this.fontWeight,
				fontColor: this.fontColor,
				fontStyle: this.fontStyle,
				textBaseline: "top"
			}), n.textBlock.measureText(), this.itemWidth !== null && (n.textBlock.width = this.itemWidth - (o + this.horizontalSpacing * .1 + (n.chartType === "line" || n.chartType === "spline" || n.chartType === "stepLine" ? 2 * this.lineHeight * .1 : 0))), (!r || r.width + Math.round(n.textBlock.width + this.horizontalSpacing * .1 + o + (r.width === 0 ? 0 : this.horizontalSpacing) + (n.chartType === "line" || n.chartType === "spline" || n.chartType === "stepLine" ? 2 * this.lineHeight * .1 : 0)) > s) && (r = {
				items: [],
				width: 0
			}, k.push(r), this.height += v, v = 0), v = Math.max(v, n.textBlock.height), n.textBlock.x = r.width, n.textBlock.y = 0, r.width += Math.round(n.textBlock.width + this.horizontalSpacing * .1 + o + (r.width === 0 ? 0 : this.horizontalSpacing) + (n.chartType === "line" || n.chartType === "spline" || n.chartType === "stepLine" ? 2 * this.lineHeight * .1 : 0)), r.items.push(n), this.width = Math.max(r.width, this.width)) : (n.textBlock = new c(this.ctx, {
				x: 0,
				y: 0,
				maxWidth: l,
				maxHeight: this.itemWrap === !0 ? h : this.fontSize * 1.5,
				angle: 0,
				text: n.text,
				horizontalAlign: "left",
				fontSize: this.fontSize,
				fontFamily: this.fontFamily,
				fontWeight: this.fontWeight,
				fontColor: this.fontColor,
				fontStyle: this.fontStyle,
				textBaseline: "top"
			}), n.textBlock.measureText(), this.itemWidth !== null && (n.textBlock.width = this.itemWidth - (o + this.horizontalSpacing * .1 + (n.chartType === "line" || n.chartType === "spline" || n.chartType === "stepLine" ? 2 * this.lineHeight * .1 : 0))), this.height <= h ? (r = {
				items: [],
				width: 0
			}, k.push(r)) : (r = k[et], et = (et + 1) % k.length), this.height += n.textBlock.height, n.textBlock.x = r.width, n.textBlock.y = 0, r.width += Math.round(n.textBlock.width + this.horizontalSpacing * .1 + o + (r.width === 0 ? 0 : this.horizontalSpacing) + (n.chartType === "line" || n.chartType === "spline" || n.chartType === "stepLine" ? 2 * this.lineHeight * .1 : 0)), r.items.push(n), this.width = Math.max(r.width, this.width)));
			this.itemWrap === !1 ? this.height = k.length * this.lineHeight : this.height += v;
			this.height = Math.min(h, this.height);
			this.width = Math.min(s, this.width)
		}
		for (this.verticalAlign === "top" ? (b = this.horizontalAlign === "left" ? f.x1 : this.horizontalAlign === "right" ? f.x2 - this.width : f.x1 + f.width / 2 - this.width / 2, w = f.y1) : this.verticalAlign === "center" ? (b = this.horizontalAlign === "left" ? f.x1 : this.horizontalAlign === "right" ? f.x2 - this.width : f.x1 + f.width / 2 - this.width / 2, w = f.y1 + f.height / 2 - this.height / 2) : this.verticalAlign === "bottom" && (b = this.horizontalAlign === "left" ? f.x1 : this.horizontalAlign === "right" ? f.x2 - this.width : f.x1 + f.width / 2 - this.width / 2, w = f.y2 - this.height), this.items = y, e = 0; e < this.items.length; e++) n = y[e], n.id = ++this.chart._eventManager.lastObjectId, this.chart._eventManager.objectMap[n.id] = {
			id: n.id,
			objectType: "legendItem",
			legendItemIndex: e,
			dataSeriesIndex: n.dataSeriesIndex,
			dataPointIndex: n.dataPointIndex
		};
		for (it = 0, e = 0; e < k.length; e++) {
			for (r = k[e], v = 0, nt = 0; nt < r.items.length; nt++) {
				var n = r.items[nt],
					tt = n.textBlock.x + b + (nt === 0 ? o * .2 : this.horizontalSpacing),
					rt = w + it,
					ut = tt;
				this.chart.data[n.dataSeriesIndex].visible || (this.ctx.globalAlpha = .5);
				this.ctx.save();
				this.ctx.rect(b, w, s, h);
				this.ctx.clip();
				(n.chartType === "line" || n.chartType === "stepLine" || n.chartType === "spline") && (this.ctx.strokeStyle = n.lineColor, this.ctx.lineWidth = Math.ceil(this.lineHeight / 8), this.ctx.beginPath(), this.ctx.moveTo(tt - this.lineHeight * .1, rt + this.lineHeight / 2), this.ctx.lineTo(tt + this.lineHeight * .7, rt + this.lineHeight / 2), this.ctx.stroke(), ut -= this.lineHeight * .1);
				a.drawMarker(tt + o / 2, rt + this.lineHeight / 2, this.ctx, n.markerType, n.markerSize, n.markerColor, n.markerBorderColor, n.markerBorderThickness);
				n.textBlock.x = tt + this.horizontalSpacing * .1 + o;
				(n.chartType === "line" || n.chartType === "stepLine" || n.chartType === "spline") && (n.textBlock.x = n.textBlock.x + this.lineHeight * .1);
				n.textBlock.y = rt;
				n.textBlock.render(!0);
				this.ctx.restore();
				v = nt > 0 ? Math.max(v, n.textBlock.height) : n.textBlock.height;
				this.chart.data[n.dataSeriesIndex].visible || (this.ctx.globalAlpha = 1);
				at = u(n.id);
				this.ghostCtx.fillStyle = at;
				this.ghostCtx.beginPath();
				this.ghostCtx.fillRect(ut, n.textBlock.y, n.textBlock.x + n.textBlock.width - ut, n.textBlock.height);
				n.x1 = this.chart._eventManager.objectMap[n.id].x1 = ut;
				n.y1 = this.chart._eventManager.objectMap[n.id].y1 = n.textBlock.y;
				n.x2 = this.chart._eventManager.objectMap[n.id].x2 = n.textBlock.x + n.textBlock.width;
				n.y2 = this.chart._eventManager.objectMap[n.id].y2 = n.textBlock.y + n.textBlock.height
			}
			it = it + v
		}
		ot.layoutManager.registerSpace(ft, {
			width: this.width + 2 + 2,
			height: this.height + 5 + 5
		});
		this.bounds = {
			x1: b,
			y1: w,
			x2: b + this.width,
			y2: w + this.height
		}
	};
	w(fi, h);
	fi.prototype.render = function() {
		var n = this.chart.layoutManager.getFreeSpace();
		this.ctx.fillStyle = "red";
		this.ctx.fillRect(n.x1, n.y1, n.x2, n.y2)
	};
	w(d, h);
	d.prototype.getDefaultAxisPlacement = function() {
		var n = this.type;
		return n === "column" || n === "line" || n === "stepLine" || n === "spline" || n === "area" || n === "stepArea" || n === "splineArea" || n === "stackedColumn" || n === "stackedLine" || n === "bubble" || n === "scatter" || n === "stackedArea" || n === "stackedColumn100" || n === "stackedLine100" || n === "stackedArea100" || n === "candlestick" || n === "ohlc" || n === "rangeColumn" || n === "rangeArea" || n === "rangeSplineArea" ? "normal" : n === "bar" || n === "stackedBar" || n === "stackedBar100" || n === "rangeBar" ? "xySwapped" : n === "pie" || n === "doughnut" || n === "funnel" ? "none" : (window.console.log("Unknown Chart Type: " + n), null)
	};
	d.getDefaultLegendMarker = function(n) {
		return n === "column" || n === "stackedColumn" || n === "stackedLine" || n === "bar" || n === "stackedBar" || n === "stackedBar100" || n === "bubble" || n === "scatter" || n === "stackedColumn100" || n === "stackedLine100" || n === "stepArea" || n === "candlestick" || n === "ohlc" || n === "rangeColumn" || n === "rangeBar" || n === "rangeArea" || n === "rangeSplineArea" ? "square" : n === "line" || n === "stepLine" || n === "spline" || n === "pie" || n === "doughnut" || n === "funnel" ? "circle" : n === "area" || n === "splineArea" || n === "stackedArea" || n === "stackedArea100" ? "triangle" : (window.console.log("Unknown Chart Type: " + n), null)
	};
	d.prototype.getDataPointAtX = function(n, t) {
		var s, h, c;
		if (!this.dataPoints || this.dataPoints.length === 0) return null;
		var i = {
				dataPoint: null,
				distance: Infinity,
				index: NaN
			},
			o = null,
			r = 0,
			u = 0,
			f = 1,
			l = Infinity,
			a = 0,
			v = 0,
			y = 1e3,
			e = 0;
		for (this.chart.plotInfo.axisPlacement !== "none" && (s = this.dataPoints[this.dataPoints.length - 1].x - this.dataPoints[0].x, e = s > 0 ? Math.min(Math.max((this.dataPoints.length - 1) / s * (n - this.dataPoints[0].x) >> 0, 0), this.dataPoints.length) : 0);;) {
			if (u = f > 0 ? e + r : e - r, u >= 0 && u < this.dataPoints.length) {
				if (o = this.dataPoints[u], h = Math.abs(o.x - n), h < i.distance && (i.dataPoint = o, i.distance = h, i.index = u), c = Math.abs(o.x - n), c <= l ? l = c : f > 0 ? a++ : v++, a > y && v > y) break
			} else if (e - r < 0 && e + r >= this.dataPoints.length) break;
			f === -1 ? (r++, f = 1) : f = -1
		}
		return t || i.dataPoint.x !== n ? t && i.dataPoint !== null ? i : null : i
	};
	d.prototype.getDataPointAtXY = function(n, t, i) {
		var et, ut, nt, a, h, d, ot, tt, v, y, w;
		if (!this.dataPoints || this.dataPoints.length === 0) return null;
		i = i || !1;
		var e = [],
			b = 0,
			f = 0,
			l = 1,
			s = !1,
			g = Infinity,
			it = 0,
			rt = 0,
			ft = 1e3,
			k = 0;
		for (this.chart.plotInfo.axisPlacement !== "none" && (et = this.chart.axisX.getXValueAt({
				x: n,
				y: t
			}), ut = this.dataPoints[this.dataPoints.length - 1].x - this.dataPoints[0].x, k = ut > 0 ? Math.min(Math.max((this.dataPoints.length - 1) / ut * (et - this.dataPoints[0].x) >> 0, 0), this.dataPoints.length) : 0);;) {
			if (f = l > 0 ? k + b : k - b, f >= 0 && f < this.dataPoints.length) {
				var st = this.dataPointIds[f],
					r = this.chart._eventManager.objectMap[st],
					o = this.dataPoints[f],
					u = null;
				if (r) {
					switch (this.type) {
						case "column":
						case "stackedColumn":
						case "stackedColumn100":
						case "bar":
						case "stackedBar":
						case "stackedBar100":
						case "rangeColumn":
						case "rangeBar":
							n >= r.x1 && n <= r.x2 && t >= r.y1 && t <= r.y2 && (e.push({
								dataPoint: o,
								dataPointIndex: f,
								dataSeries: this,
								distance: Math.min(Math.abs(r.x1 - n), Math.abs(r.x2 - n), Math.abs(r.y1 - t), Math.abs(r.y2 - t))
							}), s = !0);
							break;
						case "line":
						case "stepLine":
						case "spline":
						case "area":
						case "stepArea":
						case "stackedArea":
						case "stackedArea100":
						case "splineArea":
						case "scatter":
							h = p("markerSize", o, this) || 4;
							nt = i ? 20 : h;
							u = Math.sqrt(Math.pow(r.x1 - n, 2) + Math.pow(r.y1 - t, 2));
							u <= nt && e.push({
								dataPoint: o,
								dataPointIndex: f,
								dataSeries: this,
								distance: u
							});
							a = Math.abs(r.x1 - n);
							a <= g ? g = a : l > 0 ? it++ : rt++;
							u <= h / 2 && (s = !0);
							break;
						case "rangeArea":
						case "rangeSplineArea":
							h = p("markerSize", o, this) || 4;
							nt = i ? 20 : h;
							u = Math.min(Math.sqrt(Math.pow(r.x1 - n, 2) + Math.pow(r.y1 - t, 2)), Math.sqrt(Math.pow(r.x1 - n, 2) + Math.pow(r.y2 - t, 2)));
							u <= nt && e.push({
								dataPoint: o,
								dataPointIndex: f,
								dataSeries: this,
								distance: u
							});
							a = Math.abs(r.x1 - n);
							a <= g ? g = a : l > 0 ? it++ : rt++;
							u <= h / 2 && (s = !0);
							break;
						case "bubble":
							h = r.size;
							u = Math.sqrt(Math.pow(r.x1 - n, 2) + Math.pow(r.y1 - t, 2));
							u <= h / 2 && (e.push({
								dataPoint: o,
								dataPointIndex: f,
								dataSeries: this,
								distance: u
							}), s = !0);
							break;
						case "pie":
						case "doughnut":
							if (d = r.center, ot = this.type === "doughnut" ? .6 * r.radius : 0, u = Math.sqrt(Math.pow(d.x - n, 2) + Math.pow(d.y - t, 2)), u < r.radius && u > ot) {
								var ht = t - d.y,
									ct = n - d.x,
									c = Math.atan2(ht, ct);
								c < 0 && (c += Math.PI * 2);
								c = Number(((c / Math.PI * 180 % 360 + 360) % 360).toFixed(12));
								tt = Number(((r.startAngle / Math.PI * 180 % 360 + 360) % 360).toFixed(12));
								v = Number(((r.endAngle / Math.PI * 180 % 360 + 360) % 360).toFixed(12));
								v === 0 && r.endAngle > 1 && (v = 360);
								tt >= v && o.y !== 0 && (v += 360, c < tt && (c += 360));
								c > tt && c < v && (e.push({
									dataPoint: o,
									dataPointIndex: f,
									dataSeries: this,
									distance: 0
								}), s = !0)
							}
							break;
						case "candlestick":
							(n >= r.x1 - r.borderThickness / 2 && n <= r.x2 + r.borderThickness / 2 && t >= r.y2 - r.borderThickness / 2 && t <= r.y3 + r.borderThickness / 2 || Math.abs(r.x2 - n + r.x1 - n) < r.borderThickness && t >= r.y1 && t <= r.y4) && (e.push({
								dataPoint: o,
								dataPointIndex: f,
								dataSeries: this,
								distance: Math.min(Math.abs(r.x1 - n), Math.abs(r.x2 - n), Math.abs(r.y2 - t), Math.abs(r.y3 - t))
							}), s = !0);
							break;
						case "ohlc":
							(Math.abs(r.x2 - n + r.x1 - n) < r.borderThickness && t >= r.y2 && t <= r.y3 || n >= r.x1 && n <= (r.x2 + r.x1) / 2 && t >= r.y1 - r.borderThickness / 2 && t <= r.y1 + r.borderThickness / 2 || n >= (r.x1 + r.x2) / 2 && n <= r.x2 && t >= r.y4 - r.borderThickness / 2 && t <= r.y4 + r.borderThickness / 2) && (e.push({
								dataPoint: o,
								dataPointIndex: f,
								dataSeries: this,
								distance: Math.min(Math.abs(r.x1 - n), Math.abs(r.x2 - n), Math.abs(r.y2 - t), Math.abs(r.y3 - t))
							}), s = !0)
					}
					if (s || it > ft && rt > ft) break
				}
			} else if (k - b < 0 && k + b >= this.dataPoints.length) break;
			l === -1 ? (b++, l = 1) : l = -1
		}
		for (y = null, w = 0; w < e.length; w++) y ? e[w].distance <= y.distance && (y = e[w]) : y = e[w];
		return y
	};
	d.prototype.getMarkerProperties = function(n, t, i, r) {
		var u = this.dataPoints,
			f = this,
			e = u[n].markerColor ? u[n].markerColor : f.markerColor ? f.markerColor : u[n].color ? u[n].color : f.color ? f.color : f._colorSet[n % f._colorSet.length],
			o = u[n].markerBorderColor ? u[n].markerBorderColor : f.markerBorderColor ? f.markerBorderColor : null,
			s = u[n].markerBorderThickness ? u[n].markerBorderThickness : f.markerBorderThickness ? f.markerBorderThickness : null,
			h = u[n].markerType ? u[n].markerType : f.markerType,
			c = u[n].markerSize ? u[n].markerSize : f.markerSize;
		return {
			x: t,
			y: i,
			ctx: r,
			type: h,
			size: c,
			color: e,
			borderColor: o,
			borderThickness: s
		}
	};
	w(e, h);
	e.prototype.createLabels = function() {
		var i, n = 0,
			f, r = 0,
			u = 0,
			e = 0,
			s, o, t;
		if (this._position === "bottom" || this._position === "top" ? (e = this.lineCoordinates.width / Math.abs(this.maximum - this.minimum) * this.interval, r = this.labelAutoFit ? typeof this._options.labelMaxWidth == "undefined" ? e * .9 >> 0 : this.labelMaxWidth : typeof this._options.labelMaxWidth == "undefined" ? this.chart.width * .7 >> 0 : this.labelMaxWidth, u = typeof this._options.labelWrap == "undefined" || this.labelWrap ? this.chart.height * .5 >> 0 : this.labelFontSize * 1.5) : (this._position === "left" || this._position === "right") && (e = this.lineCoordinates.height / Math.abs(this.maximum - this.minimum) * this.interval, r = this.labelAutoFit ? typeof this._options.labelMaxWidth == "undefined" ? this.chart.width * .3 >> 0 : this.labelMaxWidth : typeof this._options.labelMaxWidth == "undefined" ? this.chart.width * .5 >> 0 : this.labelMaxWidth, u = typeof this._options.labelWrap == "undefined" || this.labelWrap ? e * 2 >> 0 : this.labelFontSize * 1.5), this.type === "axisX" && this.chart.plotInfo.axisXValueType === "dateTime")
			for (f = oi(new Date(this.maximum), this.interval, this.intervalType), n = this.intervalstartTimePercent; n < f; oi(n, this.interval, this.intervalType)) s = n.getTime(), o = this.labelFormatter ? this.labelFormatter({
				chart: this.chart,
				axis: this._options,
				value: n,
				label: this.labels[n] ? this.labels[n] : null
			}) : this.type === "axisX" && this.labels[s] ? this.labels[s] : ii(n, this.valueFormatString, this.chart._cultureInfo), i = new c(this.ctx, {
				x: 0,
				y: 0,
				maxWidth: r,
				maxHeight: u,
				angle: this.labelAngle,
				text: this.prefix + o + this.suffix,
				horizontalAlign: "left",
				fontSize: this.labelFontSize,
				fontFamily: this.labelFontFamily,
				fontWeight: this.labelFontWeight,
				fontColor: this.labelFontColor,
				fontStyle: this.labelFontStyle,
				textBaseline: "middle"
			}), this._labels.push({
				position: n.getTime(),
				textBlock: i,
				effectiveHeight: null
			});
		else {
			if (f = this.maximum, this.labels && this.labels.length) {
				var l = Math.ceil(this.interval),
					a = Math.ceil(this.intervalstartTimePercent),
					h = !1;
				for (n = a; n < this.maximum; n += l)
					if (this.labels[n]) h = !0;
					else {
						h = !1;
						break
					} h && (this.interval = l, this.intervalstartTimePercent = a)
			}
			for (n = this.intervalstartTimePercent; n <= f; n = parseFloat((n + this.interval).toFixed(14))) o = this.labelFormatter ? this.labelFormatter({
				chart: this.chart,
				axis: this._options,
				value: n,
				label: this.labels[n] ? this.labels[n] : null
			}) : this.type === "axisX" && this.labels[n] ? this.labels[n] : it(n, this.valueFormatString, this.chart._cultureInfo), i = new c(this.ctx, {
				x: 0,
				y: 0,
				maxWidth: r,
				maxHeight: u,
				angle: this.labelAngle,
				text: this.prefix + o + this.suffix,
				horizontalAlign: "left",
				fontSize: this.labelFontSize,
				fontFamily: this.labelFontFamily,
				fontWeight: this.labelFontWeight,
				fontColor: this.labelFontColor,
				fontStyle: this.labelFontStyle,
				textBaseline: "middle",
				borderThickness: 0
			}), this._labels.push({
				position: n,
				textBlock: i,
				effectiveHeight: null
			})
		}
		for (n = 0; n < this.stripLines.length; n++) t = this.stripLines[n], i = new c(this.ctx, {
			x: 0,
			y: 0,
			backgroundColor: t.labelBackgroundColor,
			maxWidth: r,
			maxHeight: u,
			angle: this.labelAngle,
			text: t.labelFormatter ? t.labelFormatter({
				chart: this.chart,
				axis: this,
				stripLine: t
			}) : t.label,
			horizontalAlign: "left",
			fontSize: t.labelFontSize,
			fontFamily: t.labelFontFamily,
			fontWeight: t.labelFontWeight,
			fontColor: t._options.labelFontColor || t.color,
			fontStyle: t.labelFontStyle,
			textBaseline: "middle",
			borderThickness: 0
		}), this._labels.push({
			position: t.value,
			textBlock: i,
			effectiveHeight: null,
			stripLine: t
		})
	};
	e.prototype.createLabelsAndCalculateWidth = function() {
		var t = 0,
			u, f;
		if (this._labels = [], this._position === "left" || this._position === "right")
			for (this.createLabels(), i = 0; i < this._labels.length; i++) {
				var e = this._labels[i].textBlock,
					r = e.measureText(),
					n = 0;
				n = this.labelAngle === 0 ? r.width : r.width * Math.cos(Math.PI / 180 * Math.abs(this.labelAngle)) + r.height / 2 * Math.sin(Math.PI / 180 * Math.abs(this.labelAngle));
				t < n && (t = n);
				this._labels[i].effectiveWidth = n
			}
		return u = this.title ? pt(this.titleFontFamily, this.titleFontSize, this.titleFontWeight) + 2 : 0, f = u + t + this.tickLength + 5, f
	};
	e.prototype.createLabelsAndCalculateHeight = function() {
		var r = 0,
			u, n, i, t, f;
		if (this._labels = [], n = 0, this.createLabels(), this._position === "bottom" || this._position === "top")
			for (n = 0; n < this._labels.length; n++) u = this._labels[n].textBlock, i = u.measureText(), t = 0, t = this.labelAngle === 0 ? i.height : i.width * Math.sin(Math.PI / 180 * Math.abs(this.labelAngle)) + i.height / 2 * Math.cos(Math.PI / 180 * Math.abs(this.labelAngle)), r < t && (r = t), this._labels[n].effectiveHeight = t;
		return f = this.title ? pt(this.titleFontFamily, this.titleFontSize, this.titleFontWeight) + 2 : 0, f + r + this.tickLength + 5
	};
	e.setLayoutAndRender = function(n, t, i, r, u) {
		var e, o, f, s, b = n.chart,
			c = b.ctx,
			a, v, k, y, p, w, d, g, h;
		n.calculateAxisParameters();
		t && t.calculateAxisParameters();
		i && i.calculateAxisParameters();
		t && i && typeof t._options.maximum == "undefined" && typeof t._options.minimum == "undefined" && typeof t._options.interval == "undefined" && typeof i._options.maximum == "undefined" && typeof i._options.minimum == "undefined" && typeof i._options.interval == "undefined" && (a = (t.maximum - t.minimum) / t.interval, v = (i.maximum - i.minimum) / i.interval, a > v ? i.maximum = i.interval * a + i.minimum : v > a && (t.maximum = t.interval * v + t.minimum));
		var nt = t ? t.lineThickness ? t.lineThickness : 0 : 0,
			tt = i ? i.lineThickness ? i.lineThickness : 0 : 0,
			it = t ? t.gridThickness ? t.gridThickness : 0 : 0,
			rt = i ? i.gridThickness ? i.gridThickness : 0 : 0,
			l = t ? t.margin : 0,
			ut = t ? t.margin : 0;
		r === "normal" ? (n.lineCoordinates = {}, k = Math.ceil(t ? t.createLabelsAndCalculateWidth() : 0), e = Math.round(u.x1 + k + l), n.lineCoordinates.x1 = e, y = Math.ceil(i ? i.createLabelsAndCalculateWidth() : 0), f = Math.round(u.x2 - y > n.chart.width - 10 ? n.chart.width - 10 : u.x2 - y), n.lineCoordinates.x2 = f, n.lineCoordinates.width = Math.abs(f - e), p = Math.ceil(n.createLabelsAndCalculateHeight()), o = Math.round(u.y2 - p - n.margin), s = Math.round(u.y2 - n.margin), n.lineCoordinates.y1 = o, n.lineCoordinates.y2 = o, n.boundingRect = {
			x1: e,
			y1: o,
			x2: f,
			y2: s,
			width: f - e,
			height: s - o
		}, t && (e = Math.round(u.x1 + t.margin), o = Math.round(u.y1 < 10 ? 10 : u.y1), f = Math.round(u.x1 + k + t.margin), s = Math.round(u.y2 - p - n.margin), t.lineCoordinates = {
			x1: f,
			y1: o,
			x2: f,
			y2: s,
			height: Math.abs(s - o)
		}, t.boundingRect = {
			x1: e,
			y1: o,
			x2: f,
			y2: s,
			width: f - e,
			height: s - o
		}), i && (e = Math.round(n.lineCoordinates.x2), o = Math.round(u.y1 < 10 ? 10 : u.y1), f = Math.round(e + y + i.margin), s = Math.round(u.y2 - p - n.margin), i.lineCoordinates = {
			x1: e,
			y1: o,
			x2: e,
			y2: s,
			height: Math.abs(s - o)
		}, i.boundingRect = {
			x1: e,
			y1: o,
			x2: f,
			y2: s,
			width: f - e,
			height: s - o
		}), n.calculateValueToPixelconversionParameters(), t && t.calculateValueToPixelconversionParameters(), i && i.calculateValueToPixelconversionParameters(), c.save(), c.rect(5, n.boundingRect.y1, n.chart.width - 10, n.boundingRect.height), c.clip(), n.renderLabelsTicksAndTitle(), c.restore(), t && t.renderLabelsTicksAndTitle(), i && i.renderLabelsTicksAndTitle(), b.preparePlotArea(), h = n.chart.plotArea, c.save(), c.rect(h.x1, h.y1, Math.abs(h.x2 - h.x1), Math.abs(h.y2 - h.y1)), c.clip(), n.renderStripLinesOfThicknessType("value"), t && t.renderStripLinesOfThicknessType("value"), i && i.renderStripLinesOfThicknessType("value"), n.renderInterlacedColors(), t && t.renderInterlacedColors(), i && i.renderInterlacedColors(), c.restore(), n.renderGrid(), t && t.renderGrid(), i && i.renderGrid(), n.renderAxisLine(), t && t.renderAxisLine(), i && i.renderAxisLine(), n.renderStripLinesOfThicknessType("pixel"), t && t.renderStripLinesOfThicknessType("pixel"), i && i.renderStripLinesOfThicknessType("pixel")) : (w = Math.ceil(n.createLabelsAndCalculateWidth()), t && (t.lineCoordinates = {}, e = Math.round(u.x1 + w + n.margin), f = Math.round(u.x2 > t.chart.width - 10 ? t.chart.width - 10 : u.x2), t.lineCoordinates.x1 = e, t.lineCoordinates.x2 = f, t.lineCoordinates.width = Math.abs(f - e)), i && (i.lineCoordinates = {}, e = Math.round(u.x1 + w + n.margin), f = Math.round(u.x2 > i.chart.width - 10 ? i.chart.width - 10 : u.x2), i.lineCoordinates.x1 = e, i.lineCoordinates.x2 = f, i.lineCoordinates.width = Math.abs(f - e)), d = Math.ceil(t ? t.createLabelsAndCalculateHeight() : 0), g = Math.ceil(i ? i.createLabelsAndCalculateHeight() : 0), t && (o = Math.round(u.y2 - d - t.margin), s = Math.round(u.y2 - l > t.chart.height - 10 ? t.chart.height - 10 : u.y2 - l), t.lineCoordinates.y1 = o, t.lineCoordinates.y2 = o, t.boundingRect = {
			x1: e,
			y1: o,
			x2: f,
			y2: s,
			width: f - e,
			height: d
		}), i && (o = Math.round(u.y1 + i.margin), s = u.y1 + i.margin + g, i.lineCoordinates.y1 = s, i.lineCoordinates.y2 = s, i.boundingRect = {
			x1: e,
			y1: o,
			x2: f,
			y2: s,
			width: f - e,
			height: g
		}), e = Math.round(u.x1 + n.margin), o = Math.round(i ? i.lineCoordinates.y2 : u.y1 < 10 ? 10 : u.y1), f = Math.round(u.x1 + w + n.margin), s = Math.round(t ? t.lineCoordinates.y1 : u.y2 - l > n.chart.height - 10 ? n.chart.height - 10 : u.y2 - l), n.lineCoordinates = {
			x1: f,
			y1: o,
			x2: f,
			y2: s,
			height: Math.abs(s - o)
		}, n.boundingRect = {
			x1: e,
			y1: o,
			x2: f,
			y2: s,
			width: f - e,
			height: s - o
		}, n.calculateValueToPixelconversionParameters(), t && t.calculateValueToPixelconversionParameters(), i && i.calculateValueToPixelconversionParameters(), t && t.renderLabelsTicksAndTitle(), i && i.renderLabelsTicksAndTitle(), n.renderLabelsTicksAndTitle(), b.preparePlotArea(), h = n.chart.plotArea, c.save(), c.rect(h.x1, h.y1, Math.abs(h.x2 - h.x1), Math.abs(h.y2 - h.y1)), c.clip(), n.renderStripLinesOfThicknessType("value"), t && t.renderStripLinesOfThicknessType("value"), i && i.renderStripLinesOfThicknessType("value"), n.renderInterlacedColors(), t && t.renderInterlacedColors(), i && i.renderInterlacedColors(), c.restore(), n.renderGrid(), t && t.renderGrid(), i && i.renderGrid(), n.renderAxisLine(), t && t.renderAxisLine(), i && i.renderAxisLine(), n.renderStripLinesOfThicknessType("pixel"), t && t.renderStripLinesOfThicknessType("pixel"), i && i.renderStripLinesOfThicknessType("pixel"))
	};
	e.prototype.renderLabelsTicksAndTitle = function() {
		var u = !1,
			o = 0,
			l = 1,
			s = 0,
			v = this.conversionParameters.pixelPerUnit * this.interval,
			h, r, f, a, t, i, n, e;
		if (this.labelAngle !== 0 && this.labelAngle !== 360 && (l = 1.2), typeof this._options.interval == "undefined") {
			if (this._position === "bottom" || this._position === "top") {
				for (n = 0; n < this._labels.length; n++)(t = this._labels[n], t.position < this.minimum || t.stripLine) || (h = t.textBlock.width * Math.cos(Math.PI / 180 * this.labelAngle) + t.textBlock.height * Math.sin(Math.PI / 180 * this.labelAngle), o += h);
				o > this.lineCoordinates.width * l && (u = !0)
			}
			if (this._position === "left" || this._position === "right") {
				for (n = 0; n < this._labels.length; n++)(t = this._labels[n], t.position < this.minimum || t.stripLine) || (h = t.textBlock.height * Math.cos(Math.PI / 180 * this.labelAngle) + t.textBlock.width * Math.sin(Math.PI / 180 * this.labelAngle), o += h);
				o > this.lineCoordinates.height * l && (u = !0)
			}
		}
		if (this._position === "bottom") {
			for (n = 0, n = 0; n < this._labels.length; n++)(t = this._labels[n], t.position < this.minimum || t.position > this.maximum) || (i = this.getPixelCoordinatesOnAxis(t.position), (this.tickThickness && !this._labels[n].stripLine || this._labels[n].stripLine && this._labels[n].stripLine._thicknessType === "pixel") && (this._labels[n].stripLine ? (r = this._labels[n].stripLine, this.ctx.lineWidth = r.thickness, this.ctx.strokeStyle = r.color) : (this.ctx.lineWidth = this.tickThickness, this.ctx.strokeStyle = this.tickColor), f = this.ctx.lineWidth % 2 == 1 ? (i.x << 0) + .5 : i.x << 0, this.ctx.beginPath(), this.ctx.moveTo(f, i.y << 0), this.ctx.lineTo(f, i.y + this.tickLength << 0), this.ctx.stroke()), !u || s++ % 2 == 0 || this._labels[n].stripLine) && (t.textBlock.angle === 0 ? (i.x -= t.textBlock.width / 2, i.y += this.tickLength + t.textBlock.fontSize / 2) : (i.x -= this.labelAngle < 0 ? t.textBlock.width * Math.cos(Math.PI / 180 * this.labelAngle) : 0, i.y += this.tickLength + Math.abs(this.labelAngle < 0 ? t.textBlock.width * Math.sin(Math.PI / 180 * this.labelAngle) - 5 : 5)), t.textBlock.x = i.x, t.textBlock.y = i.y, t.textBlock.render(!0));
			this.title && (this._titleTextBlock = new c(this.ctx, {
				x: this.lineCoordinates.x1,
				y: this.boundingRect.y2 - this.titleFontSize - 5,
				maxWidth: this.lineCoordinates.width,
				maxHeight: this.titleFontSize * 1.5,
				angle: 0,
				text: this.title,
				horizontalAlign: "center",
				fontSize: this.titleFontSize,
				fontFamily: this.titleFontFamily,
				fontWeight: this.titleFontWeight,
				fontColor: this.titleFontColor,
				fontStyle: this.titleFontStyle,
				textBaseline: "top"
			}), this._titleTextBlock.measureText(), this._titleTextBlock.x = this.lineCoordinates.x1 + this.lineCoordinates.width / 2 - this._titleTextBlock.width / 2, this._titleTextBlock.y = this.boundingRect.y2 - this._titleTextBlock.height - 3, this._titleTextBlock.render(!0))
		} else if (this._position === "top") {
			for (n = 0, n = 0; n < this._labels.length; n++)(t = this._labels[n], t.position < this.minimum || t.position > this.maximum) || (i = this.getPixelCoordinatesOnAxis(t.position), (this.tickThickness && !this._labels[n].stripLine || this._labels[n].stripLine && this._labels[n].stripLine._thicknessType === "pixel") && (this._labels[n].stripLine ? (r = this._labels[n].stripLine, this.ctx.lineWidth = r.thickness, this.ctx.strokeStyle = r.color) : (this.ctx.lineWidth = this.tickThickness, this.ctx.strokeStyle = this.tickColor), f = this.ctx.lineWidth % 2 == 1 ? (i.x << 0) + .5 : i.x << 0, this.ctx.beginPath(), this.ctx.moveTo(f, i.y << 0), this.ctx.lineTo(f, i.y - this.tickLength << 0), this.ctx.stroke()), !u || s++ % 2 == 0 || this._labels[n].stripLine) && (t.textBlock.angle === 0 ? (i.x -= t.textBlock.width / 2, i.y -= this.tickLength + t.textBlock.height / 2) : (i.x -= this.labelAngle > 0 ? t.textBlock.width * Math.cos(Math.PI / 180 * this.labelAngle) : 0, i.y -= this.tickLength + Math.abs(this.labelAngle > 0 ? t.textBlock.width * Math.sin(Math.PI / 180 * this.labelAngle) + 5 : 5)), t.textBlock.x = i.x, t.textBlock.y = i.y, t.textBlock.render(!0));
			this.title && (this._titleTextBlock = new c(this.ctx, {
				x: this.lineCoordinates.x1,
				y: this.boundingRect.y1 + 1,
				maxWidth: this.lineCoordinates.width,
				maxHeight: this.titleFontSize * 1.5,
				angle: 0,
				text: this.title,
				horizontalAlign: "center",
				fontSize: this.titleFontSize,
				fontFamily: this.titleFontFamily,
				fontWeight: this.titleFontWeight,
				fontColor: this.titleFontColor,
				fontStyle: this.titleFontStyle,
				textBaseline: "top"
			}), this._titleTextBlock.measureText(), this._titleTextBlock.x = this.lineCoordinates.x1 + this.lineCoordinates.width / 2 - this._titleTextBlock.width / 2, this._titleTextBlock.render(!0))
		} else if (this._position === "left") {
			for (n = 0; n < this._labels.length; n++)(t = this._labels[n], t.position < this.minimum || t.position > this.maximum) || (i = this.getPixelCoordinatesOnAxis(t.position), (this.tickThickness && !this._labels[n].stripLine || this._labels[n].stripLine && this._labels[n].stripLine._thicknessType === "pixel") && (this._labels[n].stripLine ? (r = this._labels[n].stripLine, this.ctx.lineWidth = r.thickness, this.ctx.strokeStyle = r.color) : (this.ctx.lineWidth = this.tickThickness, this.ctx.strokeStyle = this.tickColor), e = this.ctx.lineWidth % 2 == 1 ? (i.y << 0) + .5 : i.y << 0, this.ctx.beginPath(), this.ctx.moveTo(i.x << 0, e), this.ctx.lineTo(i.x - this.tickLength << 0, e), this.ctx.stroke()), !u || s++ % 2 == 0 || this._labels[n].stripLine) && (t.textBlock.x = i.x - t.textBlock.width * Math.cos(Math.PI / 180 * this.labelAngle) - this.tickLength - 5, t.textBlock.y = this.labelAngle === 0 ? i.y : i.y - t.textBlock.width * Math.sin(Math.PI / 180 * this.labelAngle), t.textBlock.render(!0));
			this.title && (this._titleTextBlock = new c(this.ctx, {
				x: this.boundingRect.x1 + 1,
				y: this.lineCoordinates.y2,
				maxWidth: this.lineCoordinates.height,
				maxHeight: this.titleFontSize * 1.5,
				angle: -90,
				text: this.title,
				horizontalAlign: "center",
				fontSize: this.titleFontSize,
				fontFamily: this.titleFontFamily,
				fontWeight: this.titleFontWeight,
				fontColor: this.titleFontColor,
				fontStyle: this.titleFontStyle,
				textBaseline: "top"
			}), a = this._titleTextBlock.measureText(), this._titleTextBlock.y = this.lineCoordinates.height / 2 + this._titleTextBlock.width / 2 + this.lineCoordinates.y1, this._titleTextBlock.render(!0))
		} else if (this._position === "right") {
			for (n = 0; n < this._labels.length; n++)(t = this._labels[n], t.position < this.minimum || t.position > this.maximum) || (i = this.getPixelCoordinatesOnAxis(t.position), (this.tickThickness && !this._labels[n].stripLine || this._labels[n].stripLine && this._labels[n].stripLine._thicknessType === "pixel") && (this._labels[n].stripLine ? (r = this._labels[n].stripLine, this.ctx.lineWidth = r.thickness, this.ctx.strokeStyle = r.color) : (this.ctx.lineWidth = this.tickThickness, this.ctx.strokeStyle = this.tickColor), e = this.ctx.lineWidth % 2 == 1 ? (i.y << 0) + .5 : i.y << 0, this.ctx.beginPath(), this.ctx.moveTo(i.x << 0, e), this.ctx.lineTo(i.x + this.tickLength << 0, e), this.ctx.stroke()), !u || s++ % 2 == 0 || this._labels[n].stripLine) && (t.textBlock.x = i.x + this.tickLength + 5, t.textBlock.y = this.labelAngle === 0 ? i.y : i.y, t.textBlock.render(!0));
			this.title && (this._titleTextBlock = new c(this.ctx, {
				x: this.boundingRect.x2 - 1,
				y: this.lineCoordinates.y2,
				maxWidth: this.lineCoordinates.height,
				maxHeight: this.titleFontSize * 1.5,
				angle: 90,
				text: this.title,
				horizontalAlign: "center",
				fontSize: this.titleFontSize,
				fontFamily: this.titleFontFamily,
				fontWeight: this.titleFontWeight,
				fontColor: this.titleFontColor,
				fontStyle: this.titleFontStyle,
				textBaseline: "top"
			}), this._titleTextBlock.measureText(), this._titleTextBlock.y = this.lineCoordinates.height / 2 - this._titleTextBlock.width / 2 + this.lineCoordinates.y1, this._titleTextBlock.render(!0))
		}
	};
	e.prototype.renderInterlacedColors = function() {
		var u = this.chart.plotArea.ctx,
			t, f, i = this.chart.plotArea,
			n = 0,
			r = !0;
		if ((this._position === "bottom" || this._position === "top") && this.interlacedColor)
			for (u.fillStyle = this.interlacedColor, n = 0; n < this._labels.length; n++) this._labels[n].stripLine || (r ? (t = this.getPixelCoordinatesOnAxis(this._labels[n].position), f = n + 1 >= this._labels.length - 1 ? this.getPixelCoordinatesOnAxis(this.maximum) : this.getPixelCoordinatesOnAxis(this._labels[n + 1].position), u.fillRect(t.x, i.y1, Math.abs(f.x - t.x), Math.abs(i.y1 - i.y2)), r = !1) : r = !0);
		else if ((this._position === "left" || this._position === "right") && this.interlacedColor)
			for (u.fillStyle = this.interlacedColor, n = 0; n < this._labels.length; n++) this._labels[n].stripLine || (r ? (f = this.getPixelCoordinatesOnAxis(this._labels[n].position), t = n + 1 >= this._labels.length - 1 ? this.getPixelCoordinatesOnAxis(this.maximum) : this.getPixelCoordinatesOnAxis(this._labels[n + 1].position), u.fillRect(i.x1, t.y, Math.abs(i.x1 - i.x2), Math.abs(t.y - f.y)), r = !1) : r = !0);
		u.beginPath()
	};
	e.prototype.renderStripLinesOfThicknessType = function(n) {
		var r, i, t;
		if (this.stripLines && this.stripLines.length > 0 && n)
			for (r = this, i = 0, i = 0; i < this.stripLines.length; i++)(t = this.stripLines[i], t._thicknessType === n) && (n === "pixel" && (t.value < this.minimum || t.value > this.maximum) || (t.showOnTop ? this.chart.addEventListener("dataAnimationIterationEnd", t.render, t) : t.render()))
	};
	e.prototype.renderGrid = function() {
		var n, i, r, u, t, f;
		if (this.gridThickness && this.gridThickness > 0)
			if (n = this.chart.ctx, r = this.chart.plotArea, n.lineWidth = this.gridThickness, n.strokeStyle = this.gridColor, n.setLineDash && n.setLineDash(y(this.gridDashType, this.gridThickness)), this._position === "bottom" || this._position === "top")
				for (t = 0; t < this._labels.length && !this._labels[t].stripLine; t++) this._labels[t].position < this.minimum || this._labels[t].position > this.maximum || (n.beginPath(), i = this.getPixelCoordinatesOnAxis(this._labels[t].position), u = n.lineWidth % 2 == 1 ? (i.x << 0) + .5 : i.x << 0, n.moveTo(u, r.y1 << 0), n.lineTo(u, r.y2 << 0), n.stroke());
			else if (this._position === "left" || this._position === "right")
			for (t = 0; t < this._labels.length && !this._labels[t].stripLine; t++) t === 0 && this.type === "axisY" && this.chart.axisX && this.chart.axisX.lineThickness || this._labels[t].position < this.minimum || this._labels[t].position > this.maximum || (n.beginPath(), i = this.getPixelCoordinatesOnAxis(this._labels[t].position), f = n.lineWidth % 2 == 1 ? (i.y << 0) + .5 : i.y << 0, n.moveTo(r.x1 << 0, f), n.lineTo(r.x2 << 0, f), n.stroke())
	};
	e.prototype.renderAxisLine = function() {
		var n = this.chart.ctx,
			t, i;
		this._position === "bottom" || this._position === "top" ? this.lineThickness && (n.lineWidth = this.lineThickness, n.strokeStyle = this.lineColor ? this.lineColor : "black", n.setLineDash && n.setLineDash(y(this.lineDashType, this.lineThickness)), t = this.lineThickness % 2 == 1 ? (this.lineCoordinates.y1 << 0) + .5 : this.lineCoordinates.y1 << 0, n.beginPath(), n.moveTo(this.lineCoordinates.x1, t), n.lineTo(this.lineCoordinates.x2, t), n.stroke()) : (this._position === "left" || this._position === "right") && this.lineThickness && (n.lineWidth = this.lineThickness, n.strokeStyle = this.lineColor, n.setLineDash && n.setLineDash(y(this.lineDashType, this.lineThickness)), i = this.lineThickness % 2 == 1 ? (this.lineCoordinates.x1 << 0) + .5 : this.lineCoordinates.x1 << 0, n.beginPath(), n.moveTo(i, this.lineCoordinates.y1), n.lineTo(i, this.lineCoordinates.y2), n.stroke())
	};
	e.prototype.getPixelCoordinatesOnAxis = function(n) {
		var t = {},
			r = this.lineCoordinates.width,
			u = this.lineCoordinates.height,
			i;
		return (this._position === "bottom" || this._position === "top") && (i = r / Math.abs(this.maximum - this.minimum), t.x = this.lineCoordinates.x1 + i * (n - this.minimum), t.y = this.lineCoordinates.y1), (this._position === "left" || this._position === "right") && (i = u / Math.abs(this.maximum - this.minimum), t.y = this.lineCoordinates.y2 - i * (n - this.minimum), t.x = this.lineCoordinates.x2), t
	};
	e.prototype.getXValueAt = function(n) {
		if (!n) return null;
		var t = null;
		return this._position === "left" ? t = (this.chart.axisX.maximum - this.chart.axisX.minimum) / this.chart.axisX.lineCoordinates.height * (this.chart.axisX.lineCoordinates.y2 - n.y) + this.chart.axisX.minimum : this._position === "bottom" && (t = (this.chart.axisX.maximum - this.chart.axisX.minimum) / this.chart.axisX.lineCoordinates.width * (n.x - this.chart.axisX.lineCoordinates.x1) + this.chart.axisX.minimum), t
	};
	e.prototype.calculateValueToPixelconversionParameters = function() {
		var n = {
				pixelPerUnit: null,
				minimum: null,
				reference: null
			},
			t = this.lineCoordinates.width,
			i = this.lineCoordinates.height;
		n.minimum = this.minimum;
		(this._position === "bottom" || this._position === "top") && (n.pixelPerUnit = t / Math.abs(this.maximum - this.minimum), n.reference = this.lineCoordinates.x1);
		(this._position === "left" || this._position === "right") && (n.pixelPerUnit = -1 * i / Math.abs(this.maximum - this.minimum), n.reference = this.lineCoordinates.y2);
		this.conversionParameters = n
	};
	e.prototype.calculateAxisParameters = function() {
		var h = this.chart.layoutManager.getFreeSpace(),
			l = !1,
			t, r, i, o, n, u, s, c;
		if (this._position === "bottom" || this._position === "top" ? (this.maxWidth = h.width, this.maxHeight = h.height) : (this.maxWidth = h.height, this.maxHeight = h.width), t = this.type === "axisX" ? this.maxWidth < 500 ? 8 : Math.max(6, Math.floor(this.maxWidth / 62)) : Math.max(Math.floor(this.maxWidth / 40), 2), u = 0, this.type === "axisX" ? (r = this.sessionVariables.internalMinimum !== null ? this.sessionVariables.internalMinimum : this.dataInfo.viewPortMin, i = this.sessionVariables.internalMaximum !== null ? this.sessionVariables.internalMaximum : this.dataInfo.viewPortMax, i - r == 0 && (u = typeof this._options.interval == "undefined" ? .4 : this._options.interval, i += u, r -= u), this.dataInfo.minDiff !== Infinity ? o = this.dataInfo.minDiff : i - r > 1 ? o = Math.abs(i - r) * .5 : (o = 1, this.chart.plotInfo.axisXValueType === "dateTime" && (l = !0))) : this.type === "axisY" && (r = typeof this._options.minimum == "undefined" || this._options.minimum === null ? this.dataInfo.viewPortMin : this._options.minimum, i = typeof this._options.maximum == "undefined" || this._options.maximum === null ? this.dataInfo.viewPortMax : this._options.maximum, isFinite(r) || isFinite(i) ? r === 0 && i === 0 ? (i += 9, r = 0) : i - r == 0 ? (u = Math.min(Math.abs(Math.abs(i) * .01), 5), i += u, r -= u) : r > i ? (u = Math.min(Math.abs(Math.abs(i - r) * .01), 5), i >= 0 ? r = i - u : i = r + u) : (u = Math.min(Math.abs(Math.abs(i - r) * .01), .05), i !== 0 && (i += u), r !== 0 && (r -= u)) : (i = typeof this._options.interval == "undefined" ? -Infinity : this._options.interval, r = 0), this.includeZero && (typeof this._options.minimum == "undefined" || this._options.minimum === null) && r > 0 && (r = 0), this.includeZero && (typeof this._options.maximum == "undefined" || this._options.maximum === null) && i < 0 && (i = 0)), this.type === "axisX" && this.chart.plotInfo.axisXValueType === "dateTime" ? (n = i - r, this.intervalType || (n / 1 <= t ? (this.interval = 1, this.intervalType = "millisecond") : n / 2 <= t ? (this.interval = 2, this.intervalType = "millisecond") : n / 5 <= t ? (this.interval = 5, this.intervalType = "millisecond") : n / 10 <= t ? (this.interval = 10, this.intervalType = "millisecond") : n / 20 <= t ? (this.interval = 20, this.intervalType = "millisecond") : n / 50 <= t ? (this.interval = 50, this.intervalType = "millisecond") : n / 100 <= t ? (this.interval = 100, this.intervalType = "millisecond") : n / 200 <= t ? (this.interval = 200, this.intervalType = "millisecond") : n / 250 <= t ? (this.interval = 250, this.intervalType = "millisecond") : n / 300 <= t ? (this.interval = 300, this.intervalType = "millisecond") : n / 400 <= t ? (this.interval = 400, this.intervalType = "millisecond") : n / 500 <= t ? (this.interval = 500, this.intervalType = "millisecond") : n / (f.secondDuration * 1) <= t ? (this.interval = 1, this.intervalType = "second") : n / (f.secondDuration * 2) <= t ? (this.interval = 2, this.intervalType = "second") : n / (f.secondDuration * 5) <= t ? (this.interval = 5, this.intervalType = "second") : n / (f.secondDuration * 10) <= t ? (this.interval = 10, this.intervalType = "second") : n / (f.secondDuration * 15) <= t ? (this.interval = 15, this.intervalType = "second") : n / (f.secondDuration * 20) <= t ? (this.interval = 20, this.intervalType = "second") : n / (f.secondDuration * 30) <= t ? (this.interval = 30, this.intervalType = "second") : n / (f.minuteDuration * 1) <= t ? (this.interval = 1, this.intervalType = "minute") : n / (f.minuteDuration * 2) <= t ? (this.interval = 2, this.intervalType = "minute") : n / (f.minuteDuration * 5) <= t ? (this.interval = 5, this.intervalType = "minute") : n / (f.minuteDuration * 10) <= t ? (this.interval = 10, this.intervalType = "minute") : n / (f.minuteDuration * 15) <= t ? (this.interval = 15, this.intervalType = "minute") : n / (f.minuteDuration * 20) <= t ? (this.interval = 20, this.intervalType = "minute") : n / (f.minuteDuration * 30) <= t ? (this.interval = 30, this.intervalType = "minute") : n / (f.hourDuration * 1) <= t ? (this.interval = 1, this.intervalType = "hour") : n / (f.hourDuration * 2) <= t ? (this.interval = 2, this.intervalType = "hour") : n / (f.hourDuration * 3) <= t ? (this.interval = 3, this.intervalType = "hour") : n / (f.hourDuration * 6) <= t ? (this.interval = 6, this.intervalType = "hour") : n / (f.dayDuration * 1) <= t ? (this.interval = 1, this.intervalType = "day") : n / (f.dayDuration * 2) <= t ? (this.interval = 2, this.intervalType = "day") : n / (f.dayDuration * 4) <= t ? (this.interval = 4, this.intervalType = "day") : n / (f.weekDuration * 1) <= t ? (this.interval = 1, this.intervalType = "week") : n / (f.weekDuration * 2) <= t ? (this.interval = 2, this.intervalType = "week") : n / (f.weekDuration * 3) <= t ? (this.interval = 3, this.intervalType = "week") : n / (f.monthDuration * 1) <= t ? (this.interval = 1, this.intervalType = "month") : n / (f.monthDuration * 2) <= t ? (this.interval = 2, this.intervalType = "month") : n / (f.monthDuration * 3) <= t ? (this.interval = 3, this.intervalType = "month") : n / (f.monthDuration * 6) <= t ? (this.interval = 6, this.intervalType = "month") : n / (f.yearDuration * 1) <= t ? (this.interval = 1, this.intervalType = "year") : n / (f.yearDuration * 2) <= t ? (this.interval = 2, this.intervalType = "year") : n / (f.yearDuration * 4) <= t ? (this.interval = 4, this.intervalType = "year") : (this.interval = Math.floor(e.getNiceNumber(n / (t - 1), !0) / f.yearDuration), this.intervalType = "year")), this.minimum = this.sessionVariables.internalMinimum !== null ? this.sessionVariables.internalMinimum : r - o / 2, this.maximum = this.sessionVariables.internalMaximum !== null ? this.sessionVariables.internalMaximum : i + o / 2, this.valueFormatString || (l ? this.valueFormatString = "MMM DD YYYY HH:mm" : this.intervalType === "year" ? this.valueFormatString = "YYYY" : this.intervalType === "month" ? this.valueFormatString = "MMM YYYY" : this.intervalType === "week" ? this.valueFormatString = "MMM DD YYYY" : this.intervalType === "day" ? this.valueFormatString = "MMM DD YYYY" : this.intervalType === "hour" ? this.valueFormatString = "hh:mm TT" : this.intervalType === "minute" ? this.valueFormatString = "hh:mm TT" : this.intervalType === "second" ? this.valueFormatString = "hh:mm:ss TT" : this.intervalType === "millisecond" && (this.valueFormatString = "fff'ms'")), this.intervalstartTimePercent = this.getLabelStartPoint(new Date(this.minimum), this.intervalType, this.interval)) : (this.intervalType = "number", n = e.getNiceNumber(i - r, !1), this.interval = this._options && this._options.interval ? this._options.interval : e.getNiceNumber(n / (t - 1), !0), this.minimum = this.sessionVariables.internalMinimum !== null ? this.sessionVariables.internalMinimum : Math.floor(r / this.interval) * this.interval, this.maximum = this.sessionVariables.internalMaximum !== null ? this.sessionVariables.internalMaximum : Math.ceil(i / this.interval) * this.interval, this.maximum === 0 && this.minimum === 0 && (this._options.minimum === 0 ? this.maximum += 10 : this._options.maximum === 0 && (this.minimum -= 10), this._options && typeof this._options.interval == "undefined" && (this.interval = e.getNiceNumber((this.maximum - this.minimum) / (t - 1), !0))), this.type === "axisX" ? (this.sessionVariables.internalMinimum !== null || (this.minimum = r - o / 2), this.sessionVariables.internalMaximum !== null || (this.maximum = i + o / 2), this.intervalstartTimePercent = Math.floor((this.minimum + this.interval * .2) / this.interval) * this.interval) : this.type === "axisY" && (this.intervalstartTimePercent = this.minimum)), this.type === "axisX" && (this._absoluteMinimum = this._options && typeof this._options.minimum != "undefined" ? this._options.minimum : this.dataInfo.min - o / 2, this._absoluteMaximum = this._options && typeof this._options.maximum != "undefined" ? this._options.maximum : this.dataInfo.max + o / 2), !this.valueFormatString && (this.valueFormatString = "#,##0.##", n = Math.abs(this.maximum - this.minimum), n < 1 && (s = Math.floor(Math.abs(Math.log(n) / Math.LN10)) + 2, (isNaN(s) || !isFinite(s)) && (s = 2), s > 2)))
			for (c = 0; c < s - 2; c++) this.valueFormatString += "#"
	};
	e.getNiceNumber = function(n, t) {
		var r = Math.floor(Math.log(n) / Math.LN10),
			i = n / Math.pow(10, r),
			u;
		return u = t ? i < 1.5 ? 1 : i < 3 ? 2 : i < 7 ? 5 : 10 : i <= 1 ? 1 : i <= 2 ? 2 : i <= 5 ? 5 : 10, Number((u * Math.pow(10, r)).toFixed(20))
	};
	e.prototype.getLabelStartPoint = function() {
		var t = st(this.interval, this.intervalType),
			i = Math.floor(this.minimum / t) * t,
			n = new Date(i);
		return this.intervalType === "millisecond" || (this.intervalType === "second" ? n.getMilliseconds() > 0 && (n.setSeconds(n.getSeconds() + 1), n.setMilliseconds(0)) : this.intervalType === "minute" ? (n.getSeconds() > 0 || n.getMilliseconds() > 0) && (n.setMinutes(n.getMinutes() + 1), n.setSeconds(0), n.setMilliseconds(0)) : this.intervalType === "hour" ? (n.getMinutes() > 0 || n.getSeconds() > 0 || n.getMilliseconds() > 0) && (n.setHours(n.getHours() + 1), n.setMinutes(0), n.setSeconds(0), n.setMilliseconds(0)) : this.intervalType === "day" ? (n.getHours() > 0 || n.getMinutes() > 0 || n.getSeconds() > 0 || n.getMilliseconds() > 0) && (n.setDate(n.getDate() + 1), n.setHours(0), n.setMinutes(0), n.setSeconds(0), n.setMilliseconds(0)) : this.intervalType === "week" ? (n.getDay() > 0 || n.getHours() > 0 || n.getMinutes() > 0 || n.getSeconds() > 0 || n.getMilliseconds() > 0) && (n.setDate(n.getDate() + (7 - n.getDay())), n.setHours(0), n.setMinutes(0), n.setSeconds(0), n.setMilliseconds(0)) : this.intervalType === "month" ? (n.getDate() > 1 || n.getHours() > 0 || n.getMinutes() > 0 || n.getSeconds() > 0 || n.getMilliseconds() > 0) && (n.setMonth(n.getMonth() + 1), n.setDate(1), n.setHours(0), n.setMinutes(0), n.setSeconds(0), n.setMilliseconds(0)) : this.intervalType === "year" && (n.getMonth() > 0 || n.getDate() > 1 || n.getHours() > 0 || n.getMinutes() > 0 || n.getSeconds() > 0 || n.getMilliseconds() > 0) && (n.setFullYear(n.getFullYear() + 1), n.setMonth(0), n.setDate(1), n.setHours(0), n.setMinutes(0), n.setSeconds(0), n.setMilliseconds(0))), n
	};
	w(ti, h);
	ti.prototype.render = function() {
		var n = this.parent.getPixelCoordinatesOnAxis(this.value),
			t = Math.abs(this._thicknessType === "pixel" ? this.thickness : this.parent.conversionParameters.pixelPerUnit * this.thickness),
			o, s, l, i, r, f, e, h, c;
		t > 0 && (o = this.opacity === null ? 1 : this.opacity, this.ctx.strokeStyle = this.color, this.ctx.beginPath(), s = this.ctx.globalAlpha, this.ctx.globalAlpha = o, l = u(this.id), this.ctx.lineWidth = t, this.ctx.setLineDash && this.ctx.setLineDash(y(this.lineDashType, t)), this.parent._position === "bottom" || this.parent._position === "top" ? (h = this.ctx.lineWidth % 2 == 1 ? (n.x << 0) + .5 : n.x << 0, i = r = h, f = this.chart.plotArea.y1, e = this.chart.plotArea.y2) : (this.parent._position === "left" || this.parent._position === "right") && (c = this.ctx.lineWidth % 2 == 1 ? (n.y << 0) + .5 : n.y << 0, f = e = c, i = this.chart.plotArea.x1, r = this.chart.plotArea.x2), this.ctx.moveTo(i, f), this.ctx.lineTo(r, e), this.ctx.stroke(), this.ctx.globalAlpha = s)
	};
	w(k, h);
	k.prototype._initialize = function() {
		if (this.enabled) {
			this.container = document.createElement("div");
			this.container.setAttribute("class", "canvasjs-chart-tooltip");
			this.container.style.position = "absolute";
			this.container.style.height = "auto";
			this.container.style.boxShadow = "1px 1px 2px 2px rgba(0,0,0,0.1)";
			this.container.style.zIndex = "1000";
			this.container.style.display = "none";
			var t = '<div style=" width: auto;';
			t += "height: auto;";
			t += "min-width: 50px;";
			t += "line-height: auto;";
			t += "margin: 0px 0px 0px 0px;";
			t += "padding: 5px;";
			t += "font-family: Calibri, Arial, Georgia, serif;";
			t += "font-weight: normal;";
			t += "font-style: " + (n ? "italic;" : "normal;");
			t += "font-size: 14px;";
			t += "color: #000000;";
			t += "text-shadow: 1px 1px 1px rgba(0, 0, 0, 0.1);";
			t += "text-align: left;";
			t += "border: 2px solid gray;";
			t += n ? "background: rgba(255,255,255,.9);" : "background: rgb(255,255,255);";
			t += "text-indent: 0px;";
			t += "white-space: nowrap;";
			t += "border-radius: 5px;";
			t += "-moz-user-select:none;";
			t += "-khtml-user-select: none;";
			t += "-webkit-user-select: none;";
			t += "-ms-user-select: none;";
			t += "user-select: none;";
			n || (t += "filter: alpha(opacity = 90);", t += "filter: progid:DXImageTransform.Microsoft.Shadow(Strength=3, Direction=135, Color='#666666');");
			t += '} "> Sample Tooltip<\/div>';
			this.container.innerHTML = t;
			this.contentDiv = this.container.firstChild;
			this.container.style.borderRadius = this.contentDiv.style.borderRadius;
			this.chart._canvasJSContainer.appendChild(this.container)
		}
	};
	k.prototype.mouseMoveHandler = function(n, t) {
		this._lastUpdated && (new Date).getTime() - this._lastUpdated < 40 || (this._lastUpdated = (new Date).getTime(), this._updateToolTip(n, t))
	};
	k.prototype._updateToolTip = function(t, i) {
		var o, e, p, a, v, f, h, c, y;
		if (!this.chart.disableToolTip) {
			if (typeof t == "undefined" || typeof i == "undefined") {
				if (isNaN(this._prevX) || isNaN(this._prevY)) return;
				t = this._prevX;
				i = this._prevY
			} else this._prevX = t, this._prevY = i;
			var l = null,
				u = null,
				r = [],
				s, h = 0;
			if (this.shared && this.enabled && this.chart.plotInfo.axisPlacement !== "none") {
				for (h = this.chart.plotInfo.axisPlacement === "xySwapped" ? (this.chart.axisX.maximum - this.chart.axisX.minimum) / this.chart.axisX.lineCoordinates.height * (this.chart.axisX.lineCoordinates.y2 - i) + this.chart.axisX.minimum : (this.chart.axisX.maximum - this.chart.axisX.minimum) / this.chart.axisX.lineCoordinates.width * (t - this.chart.axisX.lineCoordinates.x1) + this.chart.axisX.minimum, o = [], e = 0; e < this.chart.data.length; e++) f = this.chart.data[e].getDataPointAtX(h, !0), f && f.index >= 0 && (f.dataSeries = this.chart.data[e], f.dataPoint.y !== null && o.push(f));
				if (o.length === 0) return;
				for (o.sort(function(n, t) {
						return n.distance - t.distance
					}), p = o[0], e = 0; e < o.length; e++) o[e].dataPoint.x.valueOf() === p.dataPoint.x.valueOf() && r.push(o[e]);
				o = null
			} else {
				if (a = this.chart.getDataPointAtXY(t, i, !0), a) this.currentDataPointIndex = a.dataPointIndex, this.currentSeriesIndex = a.dataSeries.index;
				else if (n)
					if (v = ci(t, i, this.chart._eventManager.ghostCtx), v > 0 && typeof this.chart._eventManager.objectMap[v] != "undefined") {
						if (eventObject = this.chart._eventManager.objectMap[v], eventObject.objectType === "legendItem") return;
						this.currentSeriesIndex = eventObject.dataSeriesIndex;
						this.currentDataPointIndex = eventObject.dataPointIndex >= 0 ? eventObject.dataPointIndex : -1
					} else this.currentDataPointIndex = -1;
				else this.currentDataPointIndex = -1;
				if (this.currentSeriesIndex >= 0) {
					if (u = this.chart.data[this.currentSeriesIndex], f = {}, this.currentDataPointIndex >= 0) l = u.dataPoints[this.currentDataPointIndex], f.dataSeries = u, f.dataPoint = l, f.index = this.currentDataPointIndex, f.distance = Math.abs(l.x - h);
					else if (this.enabled && (u.type === "line" || u.type === "stepLine" || u.type === "spline" || u.type === "area" || u.type === "stepArea" || u.type === "splineArea" || u.type === "stackedArea" || u.type === "stackedArea100" || u.type === "rangeArea" || u.type === "rangeSplineArea" || u.type === "candlestick" || u.type === "ohlc")) h = (this.chart.axisX.maximum - this.chart.axisX.minimum) / this.chart.axisX.lineCoordinates.width * (t - this.chart.axisX.lineCoordinates.x1) + this.chart.axisX.minimum.valueOf(), f = u.getDataPointAtX(h, !0), f.dataSeries = u, this.currentDataPointIndex = f.index, l = f.dataPoint;
					else return;
					f.dataPoint.y !== null && r.push(f)
				}
			}
			if (r.length > 0 && (this.highlightObjects(r), this.enabled))
				if (c = "", c = this.getToolTipInnerHTML({
						entries: r
					}), c !== null) {
					this.contentDiv.innerHTML = c;
					this.contentDiv.innerHTML = c;
					y = !1;
					this.container.style.display === "none" && (y = !0, this.container.style.display = "block");
					try {
						this.contentDiv.style.background = this.backgroundColor ? this.backgroundColor : n ? "rgba(255,255,255,.9)" : "rgb(255,255,255)";
						this.contentDiv.style.borderRightColor = this.contentDiv.style.borderLeftColor = this.contentDiv.style.borderColor = this.borderColor ? this.borderColor : r[0].dataPoint.color ? r[0].dataPoint.color : r[0].dataSeries.color ? r[0].dataSeries.color : r[0].dataSeries._colorSet[r[0].index % r[0].dataSeries._colorSet.length];
						this.contentDiv.style.borderWidth = this.borderThickness || this.borderThickness === 0 ? this.borderThickness + "px" : "2px";
						this.contentDiv.style.borderRadius = this.cornerRadius || this.cornerRadius === 0 ? this.cornerRadius + "px" : "5px";
						this.container.style.borderRadius = this.contentDiv.style.borderRadius;
						this.contentDiv.style.fontSize = this.fontSize || this.fontSize === 0 ? this.fontSize + "px" : "14px";
						this.contentDiv.style.color = this.fontColor ? this.fontColor : "#000000";
						this.contentDiv.style.fontFamily = this.fontFamily ? this.fontFamily : "Calibri, Arial, Georgia, serif;";
						this.contentDiv.style.fontWeight = this.fontWeight ? this.fontWeight : "normal";
						this.contentDiv.style.fontStyle = this.fontStyle ? this.fontStyle : n ? "italic" : "normal"
					} catch (w) {}
					toolTipLeft = r[0].dataSeries.type === "pie" || r[0].dataSeries.type === "doughnut" || r[0].dataSeries.type === "funnel" || r[0].dataSeries.type === "bar" || r[0].dataSeries.type === "rangeBar" || r[0].dataSeries.type === "stackedBar" || r[0].dataSeries.type === "stackedBar100" ? t - 10 - this.container.clientWidth : (this.chart.axisX.lineCoordinates.width / Math.abs(this.chart.axisX.maximum - this.chart.axisX.minimum) * Math.abs(r[0].dataPoint.x - this.chart.axisX.minimum) + this.chart.axisX.lineCoordinates.x1 + .5 - this.container.clientWidth << 0) - 10;
					toolTipLeft < 0 && (toolTipLeft += this.container.clientWidth + 20);
					toolTipLeft + this.container.clientWidth > this.chart._container.clientWidth && (toolTipLeft = Math.max(0, this.chart._container.clientWidth - this.container.clientWidth));
					toolTipLeft += "px";
					s = r.length !== 1 || this.shared || r[0].dataSeries.type !== "line" && r[0].dataSeries.type !== "stepLine" && r[0].dataSeries.type !== "spline" && r[0].dataSeries.type !== "area" && r[0].dataSeries.type !== "stepArea" && r[0].dataSeries.type !== "splineArea" && r[0].dataSeries.type !== "stackedArea" && r[0].dataSeries.type !== "stackedArea100" ? r[0].dataSeries.type === "bar" || r[0].dataSeries.type === "rangeBar" || r[0].dataSeries.type === "stackedBar" || r[0].dataSeries.type === "stackedBar100" ? r[0].dataSeries.axisX.lineCoordinates.y2 - r[0].dataSeries.axisX.lineCoordinates.height / Math.abs(r[0].dataSeries.axisX.maximum - r[0].dataSeries.axisX.minimum) * Math.abs(r[0].dataPoint.x - r[0].dataSeries.axisX.minimum) + .5 << 0 : i : r[0].dataSeries.axisY.lineCoordinates.y2 - r[0].dataSeries.axisY.lineCoordinates.height / Math.abs(r[0].dataSeries.axisY.maximum - r[0].dataSeries.axisY.minimum) * Math.abs(r[0].dataPoint.y - r[0].dataSeries.axisY.minimum) + .5 << 0;
					s = -s + 10;
					s + this.container.clientHeight + 5 > 0 && (s -= s + this.container.clientHeight + 5 - 0);
					s += "px";
					this.container.style.left = toolTipLeft;
					this.container.style.bottom = s;
					!this.animationEnabled || y ? this.disableAnimation() : this.enableAnimation()
				} else this.hide(!1)
		}
	};
	k.prototype.highlightObjects = function(n) {
		var i = this.chart.overlaidCanvasCtx,
			l, f, e, s, t, u;
		for (this.chart.resetOverlayedCanvas(), i.save(), l = this.chart.plotArea, f = 0, e = 0; e < n.length; e++)
			if (s = n[e], t = this.chart._eventManager.objectMap[s.dataSeries.dataPointIds[s.index]], t && t.objectType && t.objectType === "dataPoint") {
				var r = this.chart.data[t.dataSeriesIndex],
					c = r.dataPoints[t.dataPointIndex],
					h = t.dataPointIndex;
				c.highlightEnabled !== !1 && (r.highlightEnabled === !0 || c.highlightEnabled === !0) && (r.type === "line" || r.type === "stepLine" || r.type === "spline" || r.type === "scatter" || r.type === "area" || r.type === "stepArea" || r.type === "splineArea" || r.type === "stackedArea" || r.type === "stackedArea100" || r.type === "rangeArea" || r.type === "rangeSplineArea" ? (u = r.getMarkerProperties(h, t.x1, t.y1, this.chart.overlaidCanvasCtx), u.size = Math.max(u.size * 1.5 << 0, 10), u.borderColor = u.borderColor || "#FFFFFF", u.borderThickness = u.borderThickness || Math.ceil(u.size * .1), a.drawMarkers([u]), typeof t.y2 != "undefined" && (u = r.getMarkerProperties(h, t.x1, t.y2, this.chart.overlaidCanvasCtx), u.size = Math.max(u.size * 1.5 << 0, 10), u.borderColor = u.borderColor || "#FFFFFF", u.borderThickness = u.borderThickness || Math.ceil(u.size * .1), a.drawMarkers([u]))) : r.type === "bubble" ? (u = r.getMarkerProperties(h, t.x1, t.y1, this.chart.overlaidCanvasCtx), u.size = t.size, u.color = "white", u.borderColor = "white", i.globalAlpha = .3, a.drawMarkers([u]), i.globalAlpha = 1) : r.type === "column" || r.type === "stackedColumn" || r.type === "stackedColumn100" || r.type === "bar" || r.type === "rangeBar" || r.type === "stackedBar" || r.type === "stackedBar100" || r.type === "rangeColumn" ? o(i, t.x1, t.y1, t.x2, t.y2, "white", 0, null, !1, !1, !1, !1, .3) : r.type === "pie" || r.type === "doughnut" ? dt(i, t.center, t.radius, "white", r.type, t.startAngle, t.endAngle, .3) : r.type === "candlestick" ? (i.globalAlpha = 1, i.strokeStyle = t.color, i.lineWidth = t.borderThickness * 2, f = i.lineWidth % 2 == 0 ? 0 : .5, i.beginPath(), i.moveTo(t.x3 - f, t.y2), i.lineTo(t.x3 - f, Math.min(t.y1, t.y4)), i.stroke(), i.beginPath(), i.moveTo(t.x3 - f, Math.max(t.y1, t.y4)), i.lineTo(t.x3 - f, t.y3), i.stroke(), o(i, t.x1, Math.min(t.y1, t.y4), t.x2, Math.max(t.y1, t.y4), "transparent", t.borderThickness * 2, t.color, !1, !1, !1, !1), i.globalAlpha = 1) : r.type === "ohlc" && (i.globalAlpha = 1, i.strokeStyle = t.color, i.lineWidth = t.borderThickness * 2, f = i.lineWidth % 2 == 0 ? 0 : .5, i.beginPath(), i.moveTo(t.x3 - f, t.y2), i.lineTo(t.x3 - f, t.y3), i.stroke(), i.beginPath(), i.moveTo(t.x3, t.y1), i.lineTo(t.x1, t.y1), i.stroke(), i.beginPath(), i.moveTo(t.x3, t.y4), i.lineTo(t.x2, t.y4), i.stroke(), i.globalAlpha = 1))
			} i.globalAlpha = 1;
		i.beginPath();
		return
	};
	k.prototype.getToolTipInnerHTML = function(n) {
		for (var c, s, f = n.entries, r = null, t = null, i = null, o = 0, u = "", h = !0, e = 0; e < f.length; e++)
			if (f[e].dataSeries.toolTipContent || f[e].dataPoint.toolTipContent) {
				h = !1;
				break
			} if (h && (this.content && typeof this.content == "function" || this.contentFormatter)) c = {
			chart: this.chart,
			toolTip: this._options,
			entries: f
		}, r = this.contentFormatter ? this.contentFormatter(c) : this.content(c);
		else if (this.shared && this.chart.plotInfo.axisPlacement !== "none") {
			for (s = "", e = 0; e < f.length; e++)(t = f[e].dataSeries, i = f[e].dataPoint, o = f[e].index, u = "", e === 0 && h && !this.content && (s += typeof this.chart.axisX.labels[i.x] != "undefined" ? this.chart.axisX.labels[i.x] : "{x}", s += "<\/br>", s = this.chart.replaceKeywordsWithValue(s, i, t, o)), i.toolTipContent !== null && (typeof i.toolTipContent != "undefined" || t._options.toolTipContent !== null)) && (t.type === "line" || t.type === "stepLine" || t.type === "spline" || t.type === "area" || t.type === "stepArea" || t.type === "splineArea" || t.type === "column" || t.type === "bar" || t.type === "scatter" || t.type === "stackedColumn" || t.type === "stackedColumn100" || t.type === "stackedBar" || t.type === "stackedBar100" || t.type === "stackedArea" || t.type === "stackedArea100" ? u += i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>{name}:<\/span>&nbsp;&nbsp;{y}" : t.type === "bubble" ? u += i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>{name}:<\/span>&nbsp;&nbsp;{y}, &nbsp;&nbsp;{z}" : t.type === "pie" || t.type === "doughnut" || t.type === "funnel" ? u += i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "&nbsp;&nbsp;{y}" : t.type === "rangeColumn" || t.type === "rangeBar" || t.type === "rangeArea" || t.type === "rangeSplineArea" ? u += i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>{name}:<\/span>&nbsp;&nbsp;{y[0]},&nbsp;{y[1]}" : (t.type === "candlestick" || t.type === "ohlc") && (u += i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>{name}:<\/span><br/>Open: &nbsp;&nbsp;{y[0]}<br/>High: &nbsp;&nbsp;&nbsp;{y[1]}<br/>Low:&nbsp;&nbsp;&nbsp;{y[2]}<br/>Close: &nbsp;&nbsp;{y[3]}"), r === null && (r = ""), this.reversed === !0 ? (r = this.chart.replaceKeywordsWithValue(u, i, t, o) + r, e < f.length - 1 && (r = "<\/br>" + r)) : (r += this.chart.replaceKeywordsWithValue(u, i, t, o), e < f.length - 1 && (r += "<\/br>")));
			r !== null && (r = s + r)
		} else {
			if (t = f[0].dataSeries, i = f[0].dataPoint, o = f[0].index, i.toolTipContent === null || typeof i.toolTipContent == "undefined" && t._options.toolTipContent === null) return null;
			t.type === "line" || t.type === "stepLine" || t.type === "spline" || t.type === "area" || t.type === "stepArea" || t.type === "splineArea" || t.type === "column" || t.type === "bar" || t.type === "scatter" || t.type === "stackedColumn" || t.type === "stackedColumn100" || t.type === "stackedBar" || t.type === "stackedBar100" || t.type === "stackedArea" || t.type === "stackedArea100" ? u = i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>" + (i.label ? "{label}" : "{x}") + " :<\/span>&nbsp;&nbsp;{y}" : t.type === "bubble" ? u = i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>" + (i.label ? "{label}" : "{x}") + ":<\/span>&nbsp;&nbsp;{y}, &nbsp;&nbsp;{z}" : t.type === "pie" || t.type === "doughnut" || t.type === "funnel" ? u = i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : (i.name ? "{name}:&nbsp;&nbsp;" : i.label ? "{label}:&nbsp;&nbsp;" : "") + "{y}" : t.type === "rangeColumn" || t.type === "rangeBar" || t.type === "rangeArea" || t.type === "rangeSplineArea" ? u = i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>" + (i.label ? "{label}" : "{x}") + " :<\/span>&nbsp;&nbsp;{y[0]}, &nbsp;{y[1]}" : (t.type === "candlestick" || t.type === "ohlc") && (u = i.toolTipContent ? i.toolTipContent : t.toolTipContent ? t.toolTipContent : this.content && typeof this.content != "function" ? this.content : "<span style='\"'color:{color};'\"'>" + (i.label ? "{label}" : "{x}") + "<\/span><br/>Open: &nbsp;&nbsp;{y[0]}<br/>High: &nbsp;&nbsp;&nbsp;{y[1]}<br/>Low: &nbsp;&nbsp;&nbsp;&nbsp;{y[2]}<br/>Close: &nbsp;&nbsp;{y[3]}");
			r === null && (r = "");
			r += this.chart.replaceKeywordsWithValue(u, i, t, o)
		}
		return r
	};
	k.prototype.enableAnimation = function() {
		this.container.style.WebkitTransition || (this.container.style.WebkitTransition = "left .2s ease-out, bottom .2s ease-out", this.container.style.MozTransition = "left .2s ease-out, bottom .2s ease-out", this.container.style.MsTransition = "left .2s ease-out, bottom .2s ease-out", this.container.style.transition = "left .2s ease-out, bottom .2s ease-out")
	};
	k.prototype.disableAnimation = function() {
		this.container.style.WebkitTransition && (this.container.style.WebkitTransition = "", this.container.style.MozTransition = "", this.container.style.MsTransition = "", this.container.style.transition = "")
	};
	k.prototype.hide = function(n) {
		this.enabled && (n = typeof n == "undefined" ? !0 : n, this.container.style.display = "none", this.currentSeriesIndex = -1, this._prevX = NaN, this._prevY = NaN, n && this.chart.resetOverlayedCanvas())
	};
	t.prototype.getPercentAndTotal = function(n, t) {
		var u = null,
			r = null,
			f = null;
		if (n.type.indexOf("stacked") >= 0) r = 0, u = t.x.getTime ? t.x.getTime() : t.x, u in n.plotUnit.yTotals && (r = n.plotUnit.yTotals[u], f = isNaN(t.y) ? 0 : r === 0 ? 0 : t.y / r * 100);
		else if (n.type === "pie" || n.type === "doughnut") {
			for (r = 0, i = 0; i < n.dataPoints.length; i++) isNaN(n.dataPoints[i].y) || (r += n.dataPoints[i].y);
			f = isNaN(t.y) ? 0 : t.y / r * 100
		}
		return {
			percent: f,
			total: r
		}
	};
	t.prototype.replaceKeywordsWithValue = function(n, t, i, r, u) {
		var f = this,
			e, o, l, a;
		if (u = typeof u == "undefined" ? 0 : u, (i.type.indexOf("stacked") >= 0 || i.type === "pie" || i.type === "doughnut") && (n.indexOf("#percent") >= 0 || n.indexOf("#total") >= 0)) {
			var s = "#percent",
				c = "#total",
				h = this.getPercentAndTotal(i, t);
			c = h.total ? h.total : c;
			s = isNaN(h.percent) ? s : h.percent;
			do {
				if (e = "", i.percentFormatString) e = i.percentFormatString;
				else
					for (e = "#,##0.", o = Math.max(Math.ceil(Math.log(1 / Math.abs(s)) / Math.LN10), 2), (isNaN(o) || !isFinite(o)) && (o = 2), l = 0; l < o; l++) e += "#";
				n = n.replace("#percent", it(s, e, f._cultureInfo));
				n = n.replace("#total", it(c, i.yValueFormatString ? i.yValueFormatString : "#,##0.########"))
			} while (n.indexOf("#percent") >= 0 || n.indexOf("#total") >= 0)
		}
		return a = function(n) {
			var e, h, s, c, o;
			if (n[0] === '"' && n[n.length - 1] === '"' || n[0] === "'" && n[n.length - 1] === "'") return n.slice(1, n.length - 1);
			e = ht(n.slice(1, n.length - 1));
			e = e.replace("#index", u);
			h = null;
			try {
				s = e.match(/(.*?)\s*\[\s*(.*?)\s*\]/);
				s && s.length > 0 && (h = ht(s[2]), e = ht(s[1]))
			} catch (l) {}
			if (c = null, e === "color") return t.color ? t.color : i.color ? i.color : i._colorSet[r % i._colorSet.length];
			if (t.hasOwnProperty(e)) c = t;
			else if (i.hasOwnProperty(e)) c = i;
			else return "";
			return o = c[e], h !== null && (o = o[h]), e === "x" ? f.axisX && f.plotInfo.axisXValueType === "dateTime" ? ii(o, t.xValueFormatString ? t.xValueFormatString : i.xValueFormatString ? i.xValueFormatString : f.axisX && f.axisX.valueFormatString ? f.axisX.valueFormatString : "DD MMM YY", f._cultureInfo) : it(o, t.xValueFormatString ? t.xValueFormatString : i.xValueFormatString ? i.xValueFormatString : "#,##0.########", f._cultureInfo) : e === "y" ? it(o, t.yValueFormatString ? t.yValueFormatString : i.yValueFormatString ? i.yValueFormatString : "#,##0.########", f._cultureInfo) : e === "z" ? it(o, t.zValueFormatString ? t.zValueFormatString : i.zValueFormatString ? i.zValueFormatString : "#,##0.########", f._cultureInfo) : o
		}, n.replace(/\{.*?\}|"[^"]*"|'[^']*'/g, a)
	};
	at.prototype.reset = function() {
		this.lastObjectId = 0;
		this.objectMap = [];
		this.rectangularRegionEventSubscriptions = [];
		this.previousDataPointEventObject = null;
		this.eventObjects = [];
		n && (this.ghostCtx.clearRect(0, 0, this.chart.width, this.chart.height), this.ghostCtx.beginPath())
	};
	at.prototype.getNewObjectTrackingId = function() {
		return ++this.lastObjectId
	};
	at.prototype.mouseEventHandler = function(n) {
		var t, r, o, h, c, i, l, e;
		if (n.type === "mousemove" || n.type === "click") {
			var u = [],
				f = wt(n),
				s = null;
			if (s = this.chart.getObjectAtXY(f.x, f.y, !1), s && typeof this.objectMap[s] != "undefined")
				if (t = this.objectMap[s], t.objectType === "dataPoint") {
					var r = this.chart.data[t.dataSeriesIndex],
						o = r.dataPoints[t.dataPointIndex],
						a = t.dataPointIndex;
					t.eventParameter = {
						x: f.x,
						y: f.y,
						dataPoint: o,
						dataSeries: r._options,
						dataPointIndex: a,
						dataSeriesIndex: r.index,
						chart: this.chart._publicChartReference
					};
					t.eventContext = {
						context: o,
						userContext: o,
						mouseover: "mouseover",
						mousemove: "mousemove",
						mouseout: "mouseout",
						click: "click"
					};
					u.push(t);
					t = this.objectMap[r.id];
					t.eventParameter = {
						x: f.x,
						y: f.y,
						dataPoint: o,
						dataSeries: r._options,
						dataPointIndex: a,
						dataSeriesIndex: r.index,
						chart: this.chart._publicChartReference
					};
					t.eventContext = {
						context: r,
						userContext: r._options,
						mouseover: "mouseover",
						mousemove: "mousemove",
						mouseout: "mouseout",
						click: "click"
					};
					u.push(this.objectMap[r.id])
				} else t.objectType === "legendItem" && (r = this.chart.data[t.dataSeriesIndex], o = t.dataPointIndex !== null ? r.dataPoints[t.dataPointIndex] : null, t.eventParameter = {
					x: f.x,
					y: f.y,
					dataSeries: r._options,
					dataPoint: o,
					dataPointIndex: t.dataPointIndex,
					dataSeriesIndex: t.dataSeriesIndex,
					chart: this.chart._publicChartReference
				}, t.eventContext = {
					context: this.chart.legend,
					userContext: this.chart.legend._options,
					mouseover: "itemmouseover",
					mousemove: "itemmousemove",
					mouseout: "itemmouseout",
					click: "itemclick"
				}, u.push(t));
			for (h = [], i = 0; i < this.mouseoveredObjectMaps.length; i++) {
				for (c = !0, e = 0; e < u.length; e++)
					if (u[e].id === this.mouseoveredObjectMaps[i].id) {
						c = !1;
						break
					} c ? this.fireEvent(this.mouseoveredObjectMaps[i], "mouseout", n) : h.push(this.mouseoveredObjectMaps[i])
			}
			for (this.mouseoveredObjectMaps = h, i = 0; i < u.length; i++) {
				for (l = !1, e = 0; e < this.mouseoveredObjectMaps.length; e++)
					if (u[i].id === this.mouseoveredObjectMaps[e].id) {
						l = !0;
						break
					} l || (this.fireEvent(u[i], "mouseover", n), this.mouseoveredObjectMaps.push(u[i]));
				n.type === "click" ? this.fireEvent(u[i], "click", n) : n.type === "mousemove" && this.fireEvent(u[i], "mousemove", n)
			}
		}
	};
	at.prototype.fireEvent = function(n, t, i) {
		if (n && t) {
			var f = n.eventParameter,
				u = n.eventContext,
				r = n.eventContext.userContext;
			r && u && r[u[t]] && r[u[t]].call(r, f);
			t !== "mouseout" ? r.cursor && r.cursor !== i.target.style.cursor && (i.target.style.cursor = r.cursor) : (i.target.style.cursor = this.chart._defaultCursor, delete n.eventParameter, delete n.eventContext);
			t === "click" && n.objectType === "dataPoint" && this.chart.pieDoughnutClickHandler && this.chart.pieDoughnutClickHandler.call(this.chart.data[n.dataSeriesIndex], f)
		}
	};
	w(vt, h);
	ei.prototype.animate = function(n, t, i, u, f) {
		var h = this,
			s;
		for (this.chart.isAnimating = !0, f = f || r.easing.linear, i && this.animations.push({
				startTime: (new Date).getTime() + (n ? n : 0),
				duration: t,
				animationCallback: i,
				onComplete: u
			}), s = []; this.animations.length > 0;) {
			var o = this.animations.shift(),
				c = (new Date).getTime(),
				e = 0;
			o.startTime <= c && (e = f(Math.min(c - o.startTime, o.duration), 0, 1, o.duration), e = Math.min(e, 1), (isNaN(e) || !isFinite(e)) && (e = 1));
			e < 1 && s.push(o);
			o.animationCallback(e);
			e >= 1 && o.onComplete && o.onComplete()
		}
		this.animations = s;
		this.animations.length > 0 ? this.animationRequestId = this.chart.requestAnimFrame.call(window, function() {
			h.animate.call(h)
		}) : this.chart.isAnimating = !1
	};
	ei.prototype.cancelAllAnimations = function() {
		this.animations = [];
		this.animationRequestId && this.chart.cancelRequestAnimFrame.call(window, this.animationRequestId);
		this.animationRequestId = null;
		this.chart.isAnimating = !1
	};
	var r = {
			yScaleAnimation: function(n, t) {
				if (n !== 0) {
					var i = t.dest,
						r = t.source.canvas,
						u = t.animationBase,
						f = u - u * n;
					i.drawImage(r, 0, 0, r.width, r.height, 0, f, i.canvas.width / l, n * i.canvas.height / l)
				}
			},
			xScaleAnimation: function(n, t) {
				if (n !== 0) {
					var i = t.dest,
						r = t.source.canvas,
						u = t.animationBase,
						f = u - u * n;
					i.drawImage(r, 0, 0, r.width, r.height, f, 0, n * i.canvas.width / l, i.canvas.height / l)
				}
			},
			xClipAnimation: function(n, t) {
				if (n !== 0) {
					var r = t.dest,
						i = t.source.canvas;
					r.save();
					n > 0 && r.drawImage(i, 0, 0, i.width * n, i.height, 0, 0, i.width * n / l, i.height / l);
					r.restore()
				}
			},
			fadeInAnimation: function(n, t) {
				if (n !== 0) {
					var i = t.dest,
						r = t.source.canvas;
					i.save();
					i.globalAlpha = n;
					i.drawImage(r, 0, 0, r.width, r.height, 0, 0, i.canvas.width / l, i.canvas.height / l);
					i.restore()
				}
			},
			easing: {
				linear: function(n, t, i, r) {
					return i * n / r + t
				},
				easeOutQuad: function(n, t, i, r) {
					return -i * (n /= r) * (n - 2) + t
				},
				easeOutQuart: function(n, t, i, r) {
					return -i * ((n = n / r - 1) * n * n * n - 1) + t
				},
				easeInQuad: function(n, t, i, r) {
					return i * (n /= r) * n + t
				},
				easeInQuart: function(n, t, i, r) {
					return i * (n /= r) * n * n * n + t
				}
			}
		},
		a = {
			drawMarker: function(n, t, i, r, u, f, e, o) {
				if (i) {
					var s = 1;
					i.fillStyle = f ? f : "#000000";
					i.strokeStyle = e ? e : "#000000";
					i.lineWidth = o ? o : 0;
					r === "circle" ? (i.moveTo(n, t), i.beginPath(), i.arc(n, t, u / 2, 0, Math.PI * 2, !1), f && i.fill(), o && (e ? i.stroke() : (s = i.globalAlpha, i.globalAlpha = .15, i.strokeStyle = "black", i.stroke(), i.globalAlpha = s))) : r === "square" ? (i.beginPath(), i.rect(n - u / 2, t - u / 2, u, u), f && i.fill(), o && (e ? i.stroke() : (s = i.globalAlpha, i.globalAlpha = .15, i.strokeStyle = "black", i.stroke(), i.globalAlpha = s))) : r === "triangle" ? (i.beginPath(), i.moveTo(n - u / 2, t + u / 2), i.lineTo(n + u / 2, t + u / 2), i.lineTo(n, t - u / 2), i.closePath(), f && i.fill(), o && (e ? i.stroke() : (s = i.globalAlpha, i.globalAlpha = .15, i.strokeStyle = "black", i.stroke(), i.globalAlpha = s)), i.beginPath()) : r === "cross" && (i.strokeStyle = f, o = u / 4, i.lineWidth = o, i.beginPath(), i.moveTo(n - u / 2, t - u / 2), i.lineTo(n + u / 2, t + u / 2), i.stroke(), i.moveTo(n + u / 2, t - u / 2), i.lineTo(n - u / 2, t + u / 2), i.stroke())
				}
			},
			drawMarkers: function(n) {
				for (var t, i = 0; i < n.length; i++) t = n[i], a.drawMarker(t.x, t.y, t.ctx, t.type, t.size, t.color, t.borderColor, t.borderThickness)
			}
		},
		vi = {
			Chart: function(n, i) {
				var r = new t(n, i, this);
				this.render = function() {
					r.render(this.options)
				};
				this.options = r._options
			},
			addColorSet: function(n, t) {
				tt[n] = t
			},
			addCultureInfo: function(n, t) {
				ot[n] = t
			},
			formatNumber: function(n, t, i) {
				if (i = i || "en", t = t || "#,##0.##", ot[i]) return it(n, t, new vt(i));
				throw "Unknown Culture Name";
			},
			formatDate: function(n, t, i) {
				if (i = i || "en", t = t || "DD MMM YYYY", ot[i]) return ii(n, t, new vt(i));
				throw "Unknown Culture Name";
			}
		};
	vi.Chart.version = "v1.7.0 GA";
	window.CanvasJS = vi
})();
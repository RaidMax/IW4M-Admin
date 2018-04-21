from flask_restful import Resource
from pygal.style import Style
from master import ctx
import pygal
import timeago
from math import ceil

class HistoryGraph(Resource):
    def get(self, history_count):
        try:
            custom_style = Style(
            background='transparent',
            plot_background='transparent',
            foreground='rgba(109, 118, 126, 0.3)',
            foreground_strong='rgba(109, 118, 126, 0.3)',
            foreground_subtle='rgba(109, 118, 126, 0.3)',
            opacity='0.1',
            opacity_hover='0.2',
            transition='100ms ease-in',
            colors=('#007acc', '#749363')
            )

            graph = pygal.StackedLine(
                interpolate='cubic', 
                interpolation_precision=3, 
                #x_labels_major_every=100,
                #x_labels_major_count=500, 
                stroke_style={'width': 0.4}, 
                show_dots=False, 
                show_legend=False, 
                fill=True, 
                style=custom_style, 
                disable_xml_declaration=True)
            
            instance_count = [history['time'] for history in ctx.history.instance_history][-history_count:]
            
            if len(instance_count) > 0:
                graph.x_labels = [ timeago.format(instance_count[0])]

            graph.add('Instance Count', [history['count'] for history in ctx.history.instance_history][-history_count:])
            graph.add('Client Count', [history['count'] for history in ctx.history.client_history][-history_count:])
            return { 'message' :  graph.render(), 
                     'data_points' : len(instance_count)
                   }, 200
        except Exception as e:
            return { 'message' : str(e) }, 500

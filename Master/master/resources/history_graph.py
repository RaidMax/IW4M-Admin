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
            foreground='#6c757d',
            foreground_strong='#6c757d',
            foreground_subtle='#6c757d',
            opacity='0.1',
            opacity_hover='0.2',
            transition='0ms',
            colors=('#749363','#007acc'),
            )

            graph = pygal.Line(
                stroke_style={'width': 0.4}, 
                #show_dots=False, 
                show_legend=False, 
                fill=True, 
                style=custom_style, 
                disable_xml_declaration=True)
            
            instance_count = [history['time'] for history in ctx.history.instance_history][-history_count:]
            
            if len(instance_count) > 0:
                graph.x_labels = [ timeago.format(instance_count[0])]

            instance_counts = [history['count'] for history in ctx.history.instance_history][-history_count:]
            client_counts = [history['count'] for history in ctx.history.client_history][-history_count:]
            server_counts = [history['count'] for history in ctx.history.server_history][-history_count:]

            graph.add('Client Count', client_counts)
            graph.add('Instance Count', instance_counts)
            return { 'message' :  graph.render().replace("<title>Pygal</title>", ""), 
                     'data_points' : len(instance_count),
                     'instance_count' : 0 if len(instance_counts) is 0 else instance_counts[-1],
                     'client_count' : 0 if len(client_counts) is 0 else client_counts[-1],
                     'server_count' : 0 if len(server_counts) is 0 else server_counts[-1]
                   }, 200
        except Exception as e:
            return { 'message' : str(e) }, 500

import re
import os
import time

class LogReader(object):
    def __init__(self):
        self.log_file_sizes = {}
        # (if the file changes more than this, ignore ) - 1 MB
        self.max_file_size_change = 1000000
        # (if the time between checks is greater, ignore ) - 5 minutes
        self.max_file_time_change = 1000

    def read_file(self, path):
        # prevent traversing directories
        if re.search('r^.+\.\.\\.+$', path):
            return False
        # must be a valid log path and log file
        if not re.search(r'^.+[\\|\/](userraw|mods)[\\|\/].+.log$', path):
            return False
        # set the initialze size to the current file size
        file_size = 0
        if path not in self.log_file_sizes:
            self.log_file_sizes[path] = { 
                'length' : self.file_length(path),
                'read': time.time()
            }
            return ''

        # grab the previous values
        last_length = self.log_file_sizes[path]['length']
        last_read = self.log_file_sizes[path]['read']

        # the file is being tracked already
        new_file_size = self.file_length(path)
          
        # the log size was  unable to be read (probably the wrong path)
        if new_file_size < 0:
            return False

        now = time.time()
    
        file_size_difference = new_file_size - last_length
        time_difference = now - last_read

        # update the new size and actually read the data
        self.log_file_sizes[path] = {
            'length': new_file_size,
            'read': now
        }

        # if it's been too long since we read and the amount changed is too great, discard it
        # todo: do we really want old events? maybe make this an "or"
        if file_size_difference > self.max_file_size_change and time_difference > self.max_file_time_change:
            return ''
            
        new_log_info = self.get_file_lines(path, file_size_difference)
        return new_log_info

    def get_file_lines(self, path, length):
        try:
            file_handle = open(path, 'rb')
            file_handle.seek(-length, 2)
            file_data = file_handle.read(length)
            file_handle.close()
            return file_data.decode('utf-8')
        except:
            return False
        
    def file_length(self, path):
        try:
            return os.stat(path).st_size
        except:
            return -1

reader = LogReader()

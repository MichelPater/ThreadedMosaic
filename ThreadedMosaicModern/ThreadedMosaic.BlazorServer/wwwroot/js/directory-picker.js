// Directory picker JavaScript functions
window.DirectoryPicker = {
    // Trigger click on the file input to open directory picker
    openPicker: function (inputElement) {
        if (inputElement) {
            inputElement.click();
        }
    },

    // Get files from the input element
    getFilesFromInput: function (inputElement) {
        return inputElement && inputElement.files ? inputElement.files : null;
    },

    // Extract directory path from the first file's webkitRelativePath
    getDirectoryPath: function (files) {
        if (files && files.length > 0) {
            var firstFile = files[0];
            if (firstFile.webkitRelativePath) {
                var pathParts = firstFile.webkitRelativePath.split('/');
                return pathParts[0]; // Return just the directory name
            }
            // Fallback: try to get directory name from file name
            if (firstFile.name) {
                var nameParts = firstFile.name.split('/');
                return nameParts.length > 1 ? nameParts[0] : 'Selected Directory';
            }
        }
        return null;
    },

    // Count image files based on extension
    countImageFiles: function (files) {
        if (!files) return 0;
        
        var imageExtensions = ['.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tiff', '.webp'];
        var count = 0;
        
        for (var i = 0; i < files.length; i++) {
            var fileName = files[i].name.toLowerCase();
            var hasImageExtension = imageExtensions.some(function(ext) {
                return fileName.endsWith(ext);
            });
            
            if (hasImageExtension) {
                count++;
            }
        }
        
        return count;
    },

    // Clear the file input
    clearInput: function (inputElement) {
        if (inputElement) {
            inputElement.value = '';
        }
    }
};
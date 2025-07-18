// Este script lista todos los archivos dentro de Assets/Scripts o una subcarpeta específica y guarda el resultado en scripts_list.txt

const fs = require('fs');
const path = require('path');

// Toma el argumento opcional de la línea de comandos
const subfolder = process.argv[2] || '';
const dir = path.join(__dirname, 'Assets', 'Scripts', subfolder);
const output = path.join(__dirname, 'scripts_list.txt');

function listFiles(dirPath, fileList = []) {
    if (!fs.existsSync(dirPath)) return fileList;
    const files = fs.readdirSync(dirPath);
    files.forEach(file => {
        const fullPath = path.join(dirPath, file);
        if (fs.statSync(fullPath).isDirectory()) {
            listFiles(fullPath, fileList);
        } else {
            if (!file.endsWith('.meta')) { // Ignorar archivos .meta
                fileList.push(fullPath);
            }
        }
    });
    return fileList;
}

const allFiles = listFiles(dir);
fs.writeFileSync(output, allFiles.join('\n'), 'utf8');
console.log(`Archivo generado: ${output}`);
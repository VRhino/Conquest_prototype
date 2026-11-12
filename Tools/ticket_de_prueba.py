"""Hace un ticket de batalla de prueba para la batalla de verdad (-batallaReal con -ticket), a partir de un dorado
del motor: mismos campos, pero con tropas que la base de escuadras de Conquest ya tiene.

    python Tools/ticket_de_prueba.py <salida.json> [dorado.json]

Bando atacante: un héroe de la IA con escuderos (12) y una escuadra sin héroe de lanceros (10 de 12).
Bando defensor: un héroe humano con arqueros (12) y otro de la IA con escuderos (8 de 12).
"""
import json
import sys

DORADO = '../BronzeAgeFase0/contratos/golden/MensajeDeServidor__batalla_ticket_de_caravana_con_dos.json'


def escuadra(squad_id, tropa, efectivos, heroe=None):
    e = {'squadId': squad_id, 'tropaId': tropa, 'efectivosAutorizados': efectivos, 'nivel': 1, 'formacionSeleccionada': 1}
    if heroe:
        e['heroeId'] = heroe
    return e


def participante(heroe_id, nombre, clase, es_bot, escuadras):
    p = {'heroeId': heroe_id, 'heroe': {'displayName': nombre, 'classDefinitionId': clase, 'nivel': 3}, 'escuadras': escuadras}
    if es_bot:
        p['esBot'] = True
    return p


def main():
    salida = sys.argv[1]
    mensaje = json.load(open(sys.argv[2] if len(sys.argv) > 2 else DORADO, encoding='utf-8'))
    ticket = mensaje['batalla']['ticket']
    ticket['atacante'] = {
        'capacidadMaxima': 5,
        'participantes': [participante('heroe-20', 'Bóreas', 'hacha_dos_manos', True, [escuadra('esc-40', 'sqd01', 12, 'heroe-20')])],
        'escuadrasSinHeroe': [escuadra('esc-41', 'spm01', 10)],
    }
    ticket['defensor'] = {
        'faccionId': 'f-1',
        'capacidadMaxima': 5,
        'participantes': [
            participante('heroe-3', 'Aldric', 'arco_lanza', False, [escuadra('esc-9', 'arc01', 12, 'heroe-3')]),
            participante('heroe-4', 'Cora', 'espada_escudo_mediano', True, [escuadra('esc-10', 'sqd01', 8, 'heroe-4')]),
        ],
    }
    json.dump(mensaje, open(salida, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('ticket en', salida)


if __name__ == '__main__':
    main()
